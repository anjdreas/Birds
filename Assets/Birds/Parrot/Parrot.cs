﻿using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// ReSharper disable ArrangeTypeMemberModifiers, ArrangeTypeModifiers, FieldCanBeMadeReadOnly.Global, ConvertToConstant.Global, CheckNamespace, MemberCanBePrivate.Global, UnassignedField.Global, UnusedMember.Local, UnusedMember.Global

public static class AnimatorParams
{
    public const string Speed = "Speed m:s";
}

class Parrot : MonoBehaviour
{
    private const int LabelHeight = 20;
    private static readonly string[] DebugLabels = new string[10];

    private static readonly Rect[] DebugLabelRects = DebugLabels
        .Select((str, i) => new Rect(10, 10 + i * 20, 300, LabelHeight))
        .ToArray();

    private Animator _animator;

    /// <summary>
    ///     Velocity in Local coordinates (body frame).
    /// </summary>
    private Vector3 _localVelocity;

    private Transform _prevTransform;

    // Forward velocity has little drag, but sideways or up/downwards velocity has very high drag
    // to help change the velocity vector when pitching to do a climb or a banking turn
    // Body drag of 0.1 found on page 51 in 'Modelling Bird Flight': https://books.google.no/books?id=KG86AgWwFEUC&pg=PA73&lpg=PA73&dq=bird+drag+coefficient&source=bl&ots=RuK6WpSQWJ&sig=S3HbzUEQVtMxQ69gZyKGqvXzAO0&hl=en&sa=X&ved=0ahUKEwjU7KnZrsrTAhWCbZoKHcdJDTcQ6AEIkQEwFg#v=onepage&q=bird%20drag%20coefficient&f=false
    public Vector3 BodyDragFactors = new Vector3(0.1f, 0.1f, 0.1f);

    public float MassKg = 1.0f;
    public float MaxPitchRateDps = 90;
    public float MaxRollRateDps = 180;
    public float MaxThrustN = 10;

    public float MaxYawRateDps = 90;

//    public float LiftFactor = 10f;
    public float RotateIntoWindLerpFactor = .7f;

    public float DragCoefficient = 0.001f;

//    /// <summary>
//    ///     Meters traveled forward per meter altitude lost.
//    /// </summary>
//    public float GlideRatio = 10;

    private float GetLiftCoefficient(float angleOfAttackDeg)
    {
        return DragCoefficient * 1000;
//        float x = angleOfAttackDeg;
//        float x2 = x * x;
//        float x3 = x * x2;
//        /*
//        Coefficient found by regression of graph samples:
//        http://www.xuru.org/rt/PR.asp#Manually
//-4 0.15
//0 0.9
//4 1.3
//8 1.48
//9 1.49
//10 1.48
//14 1.3
//*/
//        return 8.887064785e-5f * x3 - 9.398102638e-3f * x2 + 1.439482893e-1f * x + 8.857717866e-1f;
    }

    private float GetDragCoefficient(float angleOfAttackDeg)
    {
        return DragCoefficient;
//        float x = angleOfAttackDeg;
//        float x2 = x * x;
//        float x3 = x * x2;
//        /*
//        Coefficient found by regression of graph samples:
//        http://www.xuru.org/rt/PR.asp#Manually
//-2 .18
//0 .19
//4 .25
//8 .35
//10 .42
//12 .6
//14 1.1
//*/
//        return 7.279089406e-4f * x3 - 7.467534083e-3f * x2 + 2.119913811e-2f * x + 2.330367864e-1f;
    }

    void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        _prevTransform = transform;

        // Start with a forward velocity
        _localVelocity = Vector3.forward * 10;
    }

    void Update()
    {
        // TODO Move me to a game manager object instead
        if (Input.GetButtonDown("Reset"))
            SceneManager.LoadScene(0);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float thrustForce = (Input.GetAxis("Thrust") - Input.GetAxis("Brake")) * MaxThrustN;
        float rollDelta = -Input.GetAxis("Roll") * MaxRollRateDps * dt;
        float pitchDelta = Input.GetAxis("Pitch") * MaxPitchRateDps * dt;
        float yawDelta = Input.GetAxis("Yaw") * MaxYawRateDps * dt;

        Transform prevTransform = transform;
        Vector3 prevLocalVelocity = _localVelocity;
        Vector3 prevLocalVelocity2 = new Vector3(
            Mathf.Abs(prevLocalVelocity.x) * prevLocalVelocity.x,
            Mathf.Abs(prevLocalVelocity.y) * prevLocalVelocity.y,
            Mathf.Abs(prevLocalVelocity.z) * prevLocalVelocity.z);

        // Rotate into wind
        Vector3 prevWorldVelocity = prevTransform.TransformVector(prevLocalVelocity);
        Quaternion worldRotationIntoWind = Quaternion.LookRotation(prevWorldVelocity, prevTransform.up);
        prevTransform.rotation = Quaternion.Lerp(prevTransform.rotation, worldRotationIntoWind,
            RotateIntoWindLerpFactor * prevLocalVelocity.magnitude * dt);

//        Transform prevTransform = transform;
        float prevFwdSpeed = prevLocalVelocity.z;
        float prevFwdSpeed2 = prevFwdSpeed * prevFwdSpeed;
//        Vector3 vector3 = Vector3.Scale(prevLocalVelocity, Vector3.forward);
        int angleOfAttackSign = prevLocalVelocity.y < 0 ? +1 : -1;
        float prevAngleOfAttackDeg = prevLocalVelocity.magnitude < 0.1
            ? 0
            : angleOfAttackSign * Vector3.Angle(Vector3.forward, new Vector3(0, prevLocalVelocity.y, prevLocalVelocity.z));

        Vector3 localThrust = Vector3.forward * thrustForce;
        // Lift maxing out at 1G, to simulate bird controlling its own lift
        Vector3 localLift = Vector3.up * Mathf.Min(9.81f,
                           prevFwdSpeed2 *
                           GetLiftCoefficient(prevAngleOfAttackDeg));
        Vector3 localGravitationalForce = transform.InverseTransformVector(Vector3.down * 9.81f * MassKg);
        Vector3 localBodyDrag = -Vector3.Scale(prevLocalVelocity2, BodyDragFactors);
        Vector3 localLiftInducedDrag = Vector3.back * prevFwdSpeed2 * GetDragCoefficient(prevAngleOfAttackDeg);
        Vector3 localDrag = localBodyDrag + localLiftInducedDrag;
        //        Vector3 drag = Vector3.Scale(prevVelocity, DragFactors);
        Vector3 totalLocalForce = localThrust + localDrag + localLift + localGravitationalForce;
        Vector3 totalLocalAccel = totalLocalForce / MassKg;

        Vector3 newLocalVelocity = prevLocalVelocity + totalLocalAccel * dt;
        Vector3 rotationInputEuler = new Vector3(pitchDelta, yawDelta, rollDelta);
        Vector3 localDisplacement = newLocalVelocity * dt;
        Vector3 worldDisplacement = transform.TransformVector(localDisplacement);

        transform.position += worldDisplacement;
        transform.Rotate(rotationInputEuler, Space.Self);

        // Change velocity by acceleration.
        // Transfer old forward velocity into new forward direction>

        float newFwdSpeed = newLocalVelocity.z;
        _animator.SetFloat(AnimatorParams.Speed, newFwdSpeed);

        _prevTransform = prevTransform;
        _localVelocity = newLocalVelocity;

        Vector3 velocityKmh = _localVelocity * 3.6f;
        float forwardSpeedKmh = newFwdSpeed * 3.6f;
        var i = 0;
        DebugLabels[i++] = string.Format("Accel[{0}={1} m/s²]", totalLocalAccel, totalLocalAccel.magnitude);
        DebugLabels[i++] = string.Format("Speed[{0}={1} km/h], F.Speed[{2} km/h]", velocityKmh, velocityKmh.magnitude,
            forwardSpeedKmh);
        DebugLabels[i++] = string.Format("Angle of attack [{0}]", prevAngleOfAttackDeg);
//        _debugLabels[i++] = string.Format("Drag[{0}={1}]", dragForce, dragForce.magnitude);
//        DebugLabels[i++] = string.Format("Slip[{0}={1}]", slipForce, slipForce);
        DebugLabels[i++] = string.Format("Pitch[{0:0}], Roll[{1:0}], Heading[{2:0}]", 
            Angle360ToPlusMinus180(transform.eulerAngles.x), 
            Angle360ToPlusMinus180(transform.eulerAngles.z),
            Angle360ToPlusMinus180(transform.eulerAngles.y));
        //        DebugLabels[i++] = string.Format("Thrust axis: {0}", Input.GetAxis("Thrust"));

//        Debug.DrawLine(transform.position, transform.position + transform.TransformVector(localBodyDrag), Color.green);
//        Debug.DrawLine(transform.position, transform.position + transform.TransformVector(localLiftInducedDrag), Color.gray);
//        Debug.DrawLine(transform.position, transform.position + transform.TransformVector(localDrag), Color.yellow);
//
//        Debug.DrawLine(transform.position, transform.position + transform.TransformVector(localLift), Color.blue);
//        Debug.DrawLine(transform.position, transform.position + transform.TransformVector(localThrust), Color.cyan);
//
//        Debug.DrawLine(transform.position, transform.position + transform.TransformVector(totalLocalAccel), Color.magenta);
//        Debug.DrawLine(transform.position, transform.position + transform.TransformVector(newLocalVelocity), Color.red);
    }

    float Angle360ToPlusMinus180(float angleDeg)
    {
        return angleDeg > 180 ? angleDeg - 360 : angleDeg;
    }

    void OnGUI()
    {
        for (var i = 0; i < DebugLabels.Length; i++)
        {
            string label = DebugLabels[i];
            if (label == null) continue;

            Rect rect = DebugLabelRects[i];
            GUI.Label(rect, label);
        }
    }

//    void OnCollisionEnter(Collision collision)
//    {
//        transform.position += 10 * Vector3.up;
//        collision.
//        }

    void OnTriggerEnter(Collider col)
    {
        Debug.LogError("HIT");
        transform.position += 10 * Vector3.up;
    }
}