using System.Collections;
using UnityEngine;

// Lightning for the storm cloud over the cannon deck.
// Every so often the whole cloud flashes bright - twice, quickly, the way
// lightning flickers - with an optional light and a rumble of thunder after.
// Between flashes it does nothing at all, so it costs nothing.
[RequireComponent(typeof(ParticleSystem))]
public class StormCloud : MonoBehaviour
{
    public bool lightning = true;

    [Tooltip("Seconds between flashes, random in this range.")]
    public Vector2 secondsBetweenFlashes = new(6f, 15f);

    [Tooltip("The cloud's colour during a flash. Above 1 glows.")]
    public Color flashColor = new(2.2f, 2.2f, 2.5f, 1f);

    [Header("Optional")]
    [Tooltip("A light that blinks with the flash - lights up the deck. Keep it to one, they are not free on Quest.")]
    public Light flashLight;
    public float flashLightIntensity = 3f;

    public AudioSource thunder;
    public AudioClip[] thunderClips;

    [Tooltip("Thunder arrives after the flash, like the real thing.")]
    public Vector2 thunderDelay = new(0.4f, 1.8f);

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    // The puffs and the cloud base under them - everything flashes together
    private Renderer[] cloud;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        cloud = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();

        if (flashLight != null) flashLight.intensity = 0f;
    }

    private void OnEnable()
    {
        if (lightning) StartCoroutine(Storm());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        SetFlash(false);
    }

    private IEnumerator Storm()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(secondsBetweenFlashes.x, secondsBetweenFlashes.y));

            // Flicker: on, off, on
            SetFlash(true);
            yield return new WaitForSeconds(0.06f);
            SetFlash(false);
            yield return new WaitForSeconds(0.08f);
            SetFlash(true);
            yield return new WaitForSeconds(0.12f);
            SetFlash(false);

            if (thunder != null && thunderClips != null && thunderClips.Length > 0)
            {
                yield return new WaitForSeconds(Random.Range(thunderDelay.x, thunderDelay.y));
                thunder.PlayOneShot(thunderClips[Random.Range(0, thunderClips.Length)]);
            }
        }
    }

    private void SetFlash(bool on)
    {
        if (cloud != null)
        {
            block.SetColor(BaseColor, flashColor);

            foreach (Renderer part in cloud)
            {
                if (part == null) continue;

                // Cleared rather than reset, so the renderer stays batchable
                part.SetPropertyBlock(on ? block : null);
            }
        }

        if (flashLight != null)
            flashLight.intensity = on ? flashLightIntensity : 0f;
    }
}
