using UnityEngine;

public class CannonFire : MonoBehaviour
{
    [Header("References")]
    public CannonLoader loader;
    public Transform firePoint;
    public GameObject cannonBallPrefab;
    public Animator cannonAnimator;
    public ParticleSystem fireFX;

    [Header("Projectile")]
    public float ballLifetime = 15f;
    public float reloadCooldown = 1f;

    private AudioSource audioSource;
    private float nextFireTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void Fire()
    {
        if (!loader.ballLoaded || Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + reloadCooldown;

        if (loader.overloaded)
            Explode();
        else
            LaunchBall();

        loader.ResetCannon();
    }

    private void LaunchBall()
    {
        // Left unparented on purpose: a ball in flight must not be swung
        // around by the ship rocking underneath it.
        GameObject ball = Instantiate(cannonBallPrefab, firePoint.position, firePoint.rotation);

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = firePoint.forward * loader.GetPower();

        Destroy(ball, ballLifetime);

        Play("Fire");
    }

    private void Explode()
    {
        Play("Explode");
    }

    private void Play(string trigger)
    {
        // Stop and clear before playing: calling Play on a system that is
        // already running does not re-fire its bursts, so a second shot inside
        // the first one's tail would show nothing. True = include the children,
        // and effect prefabs are almost always a stack of several systems.
        if (fireFX != null)
        {
            fireFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fireFX.Play(true);
        }

        if (cannonAnimator != null) cannonAnimator.SetTrigger(trigger);
        if (audioSource != null) audioSource.Play();
    }
}
