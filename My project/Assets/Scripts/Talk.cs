using System.Collections;
using UnityEngine;
using TMPro;

public class Talk : MonoBehaviour
{
    [Header("References")]
    public Transform worldTextAnchor;
    public Transform cameraTextAnchor;
    public Transform playerCamera;

    public Canvas dialogueCanvas;
    public TextMeshProUGUI dialogueText;

    [Header("Look Settings")]
    public float lookAngleThreshold = 35f;
    [Tooltip("Minimum time each line stays up in total, typing included.")]
    public float lineDuration = 4f;

    [Header("Bubble Offset")]
    [Tooltip("Nudge the bubble off the world anchor. X right, Y up, Z towards the player.")]
    public Vector3 worldTextOffset;

    [Tooltip("On: X means the player's right as they look at the sailor, so the bubble sits on the same side whichever way he faces. Off: the sailor's own right.")]
    public bool offsetFacesPlayer = true;

    [Header("Typewriter")]
    public bool useTypewriter = true;

    [Header("Voice")]
    [Tooltip("Short syllable sounds - one is played at random every few letters as the text types out, like video game gibberish speech. Leave empty for silent typing.")]
    public AudioClip[] voiceClips;

    [Tooltip("This sailor's voice. Below 1 is deeper (the captain), above 1 is higher.")]
    [Range(0.5f, 2f)] public float voicePitch = 1f;

    [Tooltip("Random pitch wobble per syllable, so it does not sound like one note.")]
    [Range(0f, 0.3f)] public float voicePitchJitter = 0.1f;

    [Tooltip("A sound every this many letters. 1 is chattery, 3 is calmer.")]
    [Min(1)] public int lettersPerSound = 2;

    [Tooltip("Optional. Leave empty and one is added to the sailor, playing in 3D from where he stands.")]
    public AudioSource voiceSource;

    [Tooltip("Optional. The SFX group from the mixer, so the effects volume slider controls the voices.")]
    public UnityEngine.Audio.AudioMixerGroup voiceGroup;

    [Tooltip("Pause after a line finishes typing, before the next one starts. This is the gap between lines - raise it for slower dialogue.")]
    public float holdAfterTyping = 1.2f;

    private static readonly string[][] Portuguese =
    {
        // 0 - Hub. The hub sailor is silent now that the start menu explains
        // the game. Slot kept empty so 1-4 still match the level numbers.
        new string[0],
        // 1 - Climb
        new[]
        {
            "Nós queremos encontrar terra",
            "Tu tens de subir até ao topo do mastro",
            "Cuidado para não caíres!",
            "No topo, procura pássaros ou uma ilha",
        },
        // 2 - Astrolabe
        new[]
        {
            "Ajuda-me com a navegação",
            "Pega no quadrante",
            "Procura a Ursa Menor",
            "Aponta para a Ursa Menor com o quadrante",
        },
        // 3 - Cannon
        new[]
        {
            "Vamos praticar disparos",
            "Prepara o canhão e aponta aos alvos",
            "Ordem: esponja, pólvora, bala, dispara!",
        },
        // 4 - Spices
        new[]
        {
            "Ajuda-me a verificar a mercadoria",
            "Não incomodes os outros marujos, ou perdes tempo!",
            "Vou dizer-te o que preciso, rápido!",
        },
    };

    private static readonly string[][] English =
    {
        // 0 - Hub. Silent, see the Portuguese table.
        new string[0],
        // 1 - Climb
        new[]
        {
            "We're looking for land",
            "Your job is to climb to the top",
            "Careful you don't fall!",
            "Up top, look for birds or an island",
        },
        // 2 - Astrolabe
        new[]
        {
            "Help me with the navigation",
            "Take the quadrant",
            "Look for the Ursa Minor",
            "Point at Ursa Minor with the quadrant",
        },
        // 3 - Cannon
        new[]
        {
            "Let's practise our aim",
            "Ready the cannon and aim at the targets",
            "Order: sponge, powder, ball, fire!",
        },
        // 4 - Spices
        new[]
        {
            "Help me check over the cargo",
            "Don't bother the other sailors, or you'll lose time!",
            "This is what I need, be quick!",
        },
    };

    private Typewriter typewriter;
    private Coroutine speaking;
    private int currentSet = -1;
    private int currentLine;

    void Awake()
    {
        if (useTypewriter && dialogueText != null)
        {
            typewriter = dialogueText.GetComponent<Typewriter>();

            if (typewriter == null)
                typewriter = dialogueText.gameObject.AddComponent<Typewriter>();

            SetUpVoice();
        }
    }

    // Hands this sailor's voice to his typewriter, so each sailor can sound
    // different while sharing the same typing code
    private void SetUpVoice()
    {
        if (voiceClips == null || voiceClips.Length == 0) return;

        if (voiceSource == null)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 1f;     // from his mouth, not inside your head
            voiceSource.minDistance = 1.5f;
            voiceSource.maxDistance = 15f;
        }

        if (voiceGroup != null)
            voiceSource.outputAudioMixerGroup = voiceGroup;

        typewriter.blipSource = voiceSource;
        typewriter.blips = voiceClips;
        typewriter.basePitch = voicePitch;
        typewriter.blipPitchJitter = voicePitchJitter;
        typewriter.charactersPerBlip = lettersPerSound;
    }

    void Start()
    {
        // Keep Awake-started line
        if (speaking == null) Clear();
    }

    void OnEnable()
    {
        GameSettings.LanguageChanged += RefreshLine;
    }

    void OnDisable()
    {
        GameSettings.LanguageChanged -= RefreshLine;
    }

    void Update()
    {
        UpdateTextPosition();
    }

    void UpdateTextPosition()
    {
        if (dialogueCanvas == null || playerCamera == null) return;
        if (!dialogueCanvas.gameObject.activeInHierarchy) return;

        Vector3 directionToNPC = (transform.position - playerCamera.position).normalized;
        float angle = Vector3.Angle(playerCamera.forward, directionToNPC);

        if (angle < lookAngleThreshold && worldTextAnchor != null)
        {
            Vector3 anchor = worldTextAnchor.position;
            anchor += BubbleOffset(anchor);

            dialogueCanvas.transform.position = anchor;
            dialogueCanvas.transform.rotation =
                Quaternion.LookRotation(anchor - playerCamera.position);
        }
        else if (cameraTextAnchor != null)
        {
            dialogueCanvas.transform.position = cameraTextAnchor.position;
            dialogueCanvas.transform.rotation = cameraTextAnchor.rotation;
        }
    }

    // Where to nudge the bubble, either in the player's frame or the sailor's.
    // Flattened to the horizon so looking up or down does not slide it about.
    private Vector3 BubbleOffset(Vector3 at)
    {
        if (worldTextOffset == Vector3.zero) return Vector3.zero;

        if (!offsetFacesPlayer)
            return transform.TransformVector(worldTextOffset);

        Vector3 forward = at - playerCamera.position;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f) return Vector3.zero;

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        return right * worldTextOffset.x
             + Vector3.up * worldTextOffset.y
             - forward * worldTextOffset.z;
    }

    public void Say(int lineSet)
    {
        if (lineSet < 0 || lineSet >= Portuguese.Length) return;

        StopTalking();

        currentSet = lineSet;
        speaking = StartCoroutine(ReadLines());
    }

    public void StopTalking()
    {
        if (speaking != null)
        {
            StopCoroutine(speaking);
            speaking = null;
        }

        currentSet = -1;
        Clear();
    }

    public bool IsTalking => speaking != null;

    private IEnumerator ReadLines()
    {
        string[] lines = Lines(currentSet);

        for (currentLine = 0; currentLine < lines.Length; currentLine++)
        {
            RefreshLine();

            float shownAt = Time.time;

            if (typewriter != null)
            {
                while (typewriter.IsTyping)
                    yield return null;

                // Always at least Hold After Typing to read the finished line...
                yield return new WaitForSeconds(holdAfterTyping);
            }

            // ...and never less than Line Duration in total. With the
            // typewriter on, Line Duration used to be ignored completely,
            // which is why raising it changed nothing
            float left = lineDuration - (Time.time - shownAt);
            if (left > 0f)
                yield return new WaitForSeconds(left);
        }

        speaking = null;
        currentSet = -1;
    }

    // Redraw mid-sentence if the language is switched
    private void RefreshLine()
    {
        if (dialogueText == null || currentSet < 0) return;

        string[] lines = Lines(currentSet);

        if (currentLine < 0 || currentLine >= lines.Length) return;

        if (typewriter != null)
            typewriter.Show(lines[currentLine]);
        else
            dialogueText.text = lines[currentLine];
    }

    private static string[] Lines(int set)
    {
        string[][] table = GameSettings.Language == GameLanguage.English ? English : Portuguese;
        return set >= 0 && set < table.Length ? table[set] : new string[0];
    }

    private void Clear()
    {
        if (typewriter != null)
            typewriter.Clear();
        else if (dialogueText != null)
            dialogueText.text = "";
    }
}
