using UnityEngine;
using UnityEngine.AI;

// The work animations a character can play. Values match the "Work" parameter in InnCharacter.controller.
public enum WorkAnim
{
    None = 0,
    Pour = 1,
    Cook = 2,
    Serve = 3,
    Clean = 4,
    Knock = 5,
    Stoke = 6,
    PourBath = 7,
    MakeBed = 8,
    Sit = 9,
    Sleep = 10,
}

// Small wrapper so gameplay code says what a character is doing, not which animator
// parameters to set: PlayWork(WorkAnim.Serve), SetCarrying(true), StopWork().
// Walking speed is read from the NavMeshAgent automatically when there is one.
// Safe to call on characters without an Animator (graybox placeholders): it just does nothing.
[RequireComponent(typeof(CharacterRig))]
public class AgentAnimator : MonoBehaviour
{
    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int WorkId = Animator.StringToHash("Work");
    static readonly int CarryId = Animator.StringToHash("Carry");

    [Tooltip("Smooths walk/idle blending.")]
    [SerializeField] float speedDampTime = 0.1f;

    CharacterRig rig;
    NavMeshAgent agent;
    Animator animator;
    float speed;

    public WorkAnim CurrentWork { get; private set; }
    public bool IsCarrying { get; private set; }

    void Awake()
    {
        rig = GetComponent<CharacterRig>();
        agent = GetComponent<NavMeshAgent>();
        rig.ModelChanged += Rebind;
    }

    void Start() => Rebind(rig);

    void OnDestroy()
    {
        if (rig != null) rig.ModelChanged -= Rebind;
    }

    void Update()
    {
        if (agent != null && agent.isOnNavMesh) SetSpeed(agent.velocity.magnitude);
        else if (CanAnimate) animator.SetFloat(SpeedId, speed, speedDampTime, Time.deltaTime);
    }

    public void SetSpeed(float metresPerSecond)
    {
        speed = metresPerSecond;
        if (CanAnimate) animator.SetFloat(SpeedId, speed, speedDampTime, Time.deltaTime);
    }

    public void SetCarrying(bool carrying)
    {
        IsCarrying = carrying;
        if (CanAnimate) animator.SetBool(CarryId, carrying);
    }

    public void PlayWork(WorkAnim work)
    {
        CurrentWork = work;
        if (CanAnimate) animator.SetInteger(WorkId, (int)work);
    }

    public void StopWork() => PlayWork(WorkAnim.None);

    bool CanAnimate => animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled;

    // A new model was swapped in: point at its Animator and restore what we were doing.
    void Rebind(CharacterRig changed)
    {
        animator = changed.Animator;
        if (!CanAnimate) return;
        animator.SetFloat(SpeedId, speed);
        animator.SetInteger(WorkId, (int)CurrentWork);
        animator.SetBool(CarryId, IsCarrying);
    }
}
