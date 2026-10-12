using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// Kept this class name so the existing Panel prefab instances
// don't lose the component. Driven by InfoPointer now, not proximity.
public class ProximityWorldUIPopup : MonoBehaviour
{
    [Header("Anim")]
    [FormerlySerializedAs("worldSpaceUI")]
    [SerializeField] private GameObject anim;

    [Header("What the player points at")]
    [Tooltip("Colliders that open this panel. Empty = colliders on this object.")]
    [SerializeField] private Collider[] pointTargets;

    [Tooltip("Seconds of pointing before the panel opens.")]
    [SerializeField] private float pointTimeToOpen = 1f;

    [Header("Typewriter")]
    [Tooltip("Reveal the panel's text the way the sailors' speech does.")]
    [SerializeField] private bool useTypewriter = true;

    [Tooltip("Labels to reveal. Empty = every TMP label under this panel.")]
    [SerializeField] private TMP_Text[] typedLabels;

    [Tooltip("Seconds to wait after the panel starts opening, so the text does not race the open animation.")]
    [SerializeField] private float typeDelay = 0.25f;

    [Header("Highlight")]
    [Tooltip("PUT THE DOOR/BARREL/CANNON HERE. Stays visible, just gets tinted.")]
    [FormerlySerializedAs("highlightRoot")]
    [SerializeField] private GameObject objectToTint;

    [Tooltip("The highlight colour. With Mix, the alpha is how strongly the object turns this colour.")]
    [SerializeField] private Color highlightTint = new Color(2f, 1.8f, 1.1f);

    public enum TintBlend
    {
        [Tooltip("Blend the object's colour toward the tint, by the tint's alpha. Works with any colour.")]
        Mix,

        [Tooltip("Multiply by the tint. Only brightens with values above 1 - a dark tint darkens the object.")]
        Multiply,
    }

    [SerializeField] private TintBlend tintBlend = TintBlend.Mix;

    [Tooltip("ONLY for a dedicated outline/glow mesh. This object is HIDDEN " +
             "until pointed at - do not put the real object here.")]
    [FormerlySerializedAs("highlightObject")]
    [SerializeField] private GameObject outlineToShow;

    // Different shaders name their main colour differently, and the names are
    // case-sensitive: URP Lit uses _BaseColor, older shaders _Color, and the
    // shader Unity gives 3ds Max imports (the old bed) _BASE_COLOR. Setting a
    // name the shader does not have does nothing at all, silently.
    private static readonly string[] ColorProperties =
        { "_BaseColor", "_Color", "_BASE_COLOR", "_MainColor", "_TintColor", "_Tint" };

    // Collider -> panel, so InfoPointer can find us from any hit
    private static readonly Dictionary<Collider, ProximityWorldUIPopup> Registry = new();

    // One per material, not per renderer: a renderer with several materials
    // needs each tinted from its own colour, not all painted the first one's
    private struct TintSlot
    {
        public Renderer renderer;
        public int materialIndex;
        public int property;
        public Color original;
    }

    private readonly List<TintSlot> tintSlots = new();
    private MaterialPropertyBlock block;

    private Animator animator;
    private Typewriter[] typewriters;
    private Coroutine typing;
    private bool pointedAt;
    private int pointers;
    private bool shown;
    private float pointTimer;

    public static ProximityWorldUIPopup FindByCollider(Collider col)
    {
        if (col == null) return null;

        if (Registry.TryGetValue(col, out ProximityWorldUIPopup panel) && panel != null)
            return panel;

        return col.GetComponentInParent<ProximityWorldUIPopup>();
    }

    private void Awake()
    {
        if (anim == null)
            anim = gameObject;

        // Animator often sits on a child
        animator = anim.GetComponent<Animator>();

        if (animator == null)
            animator = anim.GetComponentInChildren<Animator>(true);

        pointTargets = CleanTargets(pointTargets);

        CacheHighlight();
        CacheTypewriters();
        DrawTextAboveBackground();
    }

    // The background is a SpriteRenderer, not part of the canvas, so Unity
    // treats it and the canvas as two separate see-through objects and sorts
    // them by distance. Both sat at order 0 - a tie - so from the front the
    // text was nearer and drawn on top, but from the side the background's
    // centre won and was drawn over the text. Pin the canvas above every
    // sprite in the panel so it always draws last.
    private void DrawTextAboveBackground()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (canvas.sortingLayerID != sprite.sortingLayerID)
                canvas.sortingLayerID = sprite.sortingLayerID;

            if (canvas.sortingOrder <= sprite.sortingOrder)
                canvas.sortingOrder = sprite.sortingOrder + 1;
        }
    }

    private void CacheTypewriters()
    {
        if (!useTypewriter)
        {
            typewriters = new Typewriter[0];
            return;
        }

        TMP_Text[] labels = typedLabels != null && typedLabels.Length > 0
            ? typedLabels
            : GetComponentsInChildren<TMP_Text>(true);

        List<Typewriter> found = new List<Typewriter>();

        foreach (TMP_Text label in labels)
        {
            if (label == null) continue;

            Typewriter writer = label.GetComponent<Typewriter>();

            if (writer == null)
                writer = label.gameObject.AddComponent<Typewriter>();

            // Start hidden, so an unopened panel is not showing its text
            writer.HideAll();
            found.Add(writer);
        }

        typewriters = found.ToArray();
    }

    // Resizing the array in the inspector without filling it leaves
    // nulls, and a null-but-non-empty list registered nothing at all
    private Collider[] CleanTargets(Collider[] assigned)
    {
        List<Collider> kept = new List<Collider>();

        if (assigned != null)
        {
            foreach (Collider col in assigned)
            {
                if (col != null) kept.Add(col);
            }
        }

        if (kept.Count > 0)
            return kept.ToArray();

        Debug.LogWarning(
            "Info panel has no Point Targets assigned, so it can only be opened by " +
            "pointing at the panel itself. Drag the object's collider in.", this);

        return GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        foreach (Collider col in pointTargets)
        {
            if (col != null) Registry[col] = this;
        }
    }

    private void OnDisable()
    {
        foreach (Collider col in pointTargets)
        {
            if (col != null) Registry.Remove(col);
        }

        pointers = 0;
        pointedAt = false;
        shown = false;
        pointTimer = 0f;
        SetHighlight(false);
        StopTyping();
    }

    private void CacheHighlight()
    {
        block = new MaterialPropertyBlock();
        tintSlots.Clear();

        // UI Images are not Renderers, so the panel itself has none.
        // The object worth tinting has to be assigned explicitly.
        Renderer[] renderers = objectToTint != null
            ? objectToTint.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;

                string property = FindColorProperty(material);

                if (property == null)
                {
                    Debug.LogWarning(
                        $"Info panel: '{material.name}' on '{renderer.name}' uses shader " +
                        $"'{material.shader.name}', which has no colour property the highlight " +
                        "knows, so that part will not tint.", this);
                    continue;
                }

                tintSlots.Add(new TintSlot
                {
                    renderer = renderer,
                    materialIndex = i,
                    property = Shader.PropertyToID(property),
                    original = material.GetColor(property),
                });
            }
        }

        if (tintSlots.Count == 0 && outlineToShow == null)
        {
            Debug.LogWarning(
                "Info panel has nothing to highlight - set Object To Tint to the " +
                "door/barrel/etc, not the UI panel.", this);
        }

        if (outlineToShow != null)
            outlineToShow.SetActive(false);
    }

    private static string FindColorProperty(Material material)
    {
        foreach (string property in ColorProperties)
        {
            if (material.HasProperty(property)) return property;
        }

        return null;
    }

    // Called by InfoPointer - one per hand, so counted: one hand looking away
    // must not close a panel the other hand is still pointing at
    public void SetPointedAt(bool value)
    {
        pointers = Mathf.Max(0, pointers + (value ? 1 : -1));
        value = pointers > 0;

        if (pointedAt == value) return;

        pointedAt = value;
        pointTimer = 0f;

        SetHighlight(value);

        if (!value && shown) Hide();
    }

    private void Update()
    {
        if (!pointedAt || shown) return;

        pointTimer += Time.deltaTime;

        if (pointTimer >= pointTimeToOpen)
            Show();
    }

    // Property block, so no material instances are created
    private void SetHighlight(bool on)
    {
        if (outlineToShow != null)
            outlineToShow.SetActive(on);

        foreach (TintSlot slot in tintSlots)
        {
            if (slot.renderer == null) continue;

            // Off: remove the block entirely rather than writing the original
            // colour back. A leftover block keeps the renderer out of URP's
            // SRP Batcher for good, which costs draw-call speed for nothing.
            if (!on)
            {
                slot.renderer.SetPropertyBlock(null, slot.materialIndex);
                continue;
            }

            Color tinted = tintBlend == TintBlend.Mix
                ? Color.Lerp(slot.original,
                             new Color(highlightTint.r, highlightTint.g, highlightTint.b, slot.original.a),
                             highlightTint.a)
                : slot.original * highlightTint;

            block.Clear();
            block.SetColor(slot.property, tinted);
            slot.renderer.SetPropertyBlock(block, slot.materialIndex);
        }
    }

    private void Show()
    {
        shown = true;
        Trigger("InfoIn");

        StopTyping();

        if (typewriters.Length > 0)
            typing = StartCoroutine(TypeAfterDelay());
    }

    private void Hide()
    {
        shown = false;
        Trigger("InfoOut");

        StopTyping();

        // Hide the words, never erase them - LocalizedText owns the string
        foreach (Typewriter writer in typewriters)
        {
            if (writer != null) writer.HideAll();
        }
    }

    private IEnumerator TypeAfterDelay()
    {
        if (typeDelay > 0f)
            yield return new WaitForSeconds(typeDelay);

        foreach (Typewriter writer in typewriters)
        {
            if (writer != null) writer.Replay();
        }

        typing = null;
    }

    private void StopTyping()
    {
        if (typing == null) return;

        StopCoroutine(typing);
        typing = null;
    }

    private void Trigger(string trigger)
    {
        if (animator == null)
        {
            Debug.LogWarning("No Animator found for the info popup.", this);
            return;
        }

        animator.ResetTrigger(trigger == "InfoIn" ? "InfoOut" : "InfoIn");
        animator.SetTrigger(trigger);
    }
}
