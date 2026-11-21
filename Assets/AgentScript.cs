using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
using UnityEngine.InputSystem; // Add this namespace at the top
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Profiling.Memory.Experimental;


public class AgentScript : Agent
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    Rigidbody rBody;
    Collider objCollider;

    public float pushDistance = 32f;

    public int maxStep = 1000;

    public const int MAX_VELOCITY = 35;

    private const int AREA_RADIUS = 50;

    private Vector3 target_location = new Vector3(0, 0, 0);

    public Vector3 InstantaneousAcceleration { get; private set; }
    private Vector3 previousVelocity;

    [Header("Ray Force Settings")]
    [Tooltip("The strength of the incoming random ray force.")]
    public float forceMagnitude = 15f;

    [Tooltip("Impulse delivers a sudden impact. Use Force for a continuous push.")]
    public ForceMode forceMode = ForceMode.Impulse;

    void Start()
    {
        rBody = GetComponent<Rigidbody>();
        objCollider = GetComponent<Collider>();
        previousVelocity = rBody.linearVelocity;
    }

    void OnDrawGizmos()
    {
        // Fetch the Rigidbody component if we don't have it yet
        if (rBody == null)
        {
            rBody = GetComponent<Rigidbody>();
        }

        // Ensure the Rigidbody exists before drawing
        if (rBody != null)
        {
            // Set the color of the gizmo (Wireframe sphere)
            Gizmos.color = Color.green;

            Vector3 localCOM = rBody.centerOfMass;

            // Convert the local center of mass to world space coordinates
            Vector3 worldCenterOfMass = transform.TransformPoint(localCOM);

            // Draw a wireframe sphere with a radius of 1.0 (Unit Sphere)
            Gizmos.DrawWireSphere(worldCenterOfMass, 1.0f);

            // Optional: Draw a small solid sphere directly at the center point
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(worldCenterOfMass, 0.1f);

            Gizmos.color = Color.blue;
            Vector3 worldTargetLocation = transform.parent.TransformPoint(target_location);
            Gizmos.DrawWireSphere(worldTargetLocation, 4.0f);
        }
    }

    public override void OnEpisodeBegin()
    {
        this.rBody.angularVelocity = Vector3.zero;
        this.rBody.linearVelocity = new Vector3(0, (Random.value - (float)0.15) * 10, 0);
        this.rBody.rotation = Quaternion.identity;
        this.transform.localPosition = new Vector3(0, 25, 0);


        float maxCOMOffset = 0.15f;
        this.rBody.centerOfMass = new Vector3(Random.value * maxCOMOffset * 2 - maxCOMOffset, Random.value * maxCOMOffset * 2 - maxCOMOffset, Random.value * maxCOMOffset * 2 - maxCOMOffset);

        target_location = new Vector3(Random.value * AREA_RADIUS * 2 - AREA_RADIUS, Random.value * 15 + 15, Random.value * AREA_RADIUS * 2 - AREA_RADIUS);

        Physics.SyncTransforms();

        ApplyEpisodeStartRayForce();
    }

    private void ApplyEpisodeStartRayForce()
    {
        if (this.objCollider == null)
        {
            Debug.LogWarning("AgentScript: No Collider found on this object. Cannot calculate surface point.");
            return;
        }

        // Generate a random origin outside the agent based on its new position
        Vector3 randomSphereDirection = Random.onUnitSphere;
        Vector3 rayOrigin = transform.position + (randomSphereDirection * 20f);

        // Target the center of the agent
        Vector3 rayDirection = (transform.position - rayOrigin).normalized;

        Ray ray = new Ray(rayOrigin, rayDirection);

        // Fire a raycast inward to locate the outer shell/surface point
        if (this.objCollider.Raycast(ray, out RaycastHit hit, 50f))
        {
            Vector3 forceVector = rayDirection * forceMagnitude;
            rBody.AddForceAtPosition(forceVector, hit.point, forceMode);

            // Optional: Draws a line in the Scene view during training/testing
            Debug.DrawRay(rayOrigin, rayDirection * 20f, Color.red, 1.0f);
        }
    }

    public static float GetCosineSimilarity(Vector3 vectorA, Vector3 vectorB)
    {
        // 1. Calculate the dot product
        float dotProduct = Vector3.Dot(vectorA, vectorB);

        // 2. Calculate the magnitudes (lengths)
        float magnitudeA = vectorA.magnitude;
        float magnitudeB = vectorB.magnitude;

        // Prevent division by zero if either vector has a length of 0
        if (magnitudeA == 0 || magnitudeB == 0) return 0f;

        // 3. Divide dot product by the product of magnitudes
        return dotProduct / (magnitudeA * magnitudeB);
    }

    public float getReward()
    {


        Transform arenaTransform = this.transform.parent;
        // 1. Calculate Target World Position
        Vector3 targetWorldPos = arenaTransform.TransformPoint(target_location);

        // 2. Check Distance to Target (World Space)
        float distanceToTarget = Vector3.Distance(this.transform.position, targetWorldPos);

        if (distanceToTarget <= 5.0f)
        {
            // Big completion bonus for successfully arriving
            AddReward(10.0f);
            print("Target location reached by agent");
            EndEpisode();
            return 10.0f; // Return out immediately to stop calculating velocity
        }




        // 3. Calculate heading vector in World Space
        Vector3 directionToTarget = (targetWorldPos - this.transform.position).normalized;

        // 4. Isolate velocity direction in World Space
        Vector3 velocityDirection = this.rBody.linearVelocity.normalized;
        float cosineSim = Vector3.Dot(directionToTarget, velocityDirection) + 1;


        float stability = Vector3.Dot(this.rBody.transform.up, Vector3.up);



        return cosineSim + Mathf.Max(0, stability) * 0.5f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {

        sensor.AddObservation(rBody.rotation);
        sensor.AddObservation(rBody.angularVelocity);
        sensor.AddObservation(rBody.linearVelocity);
        sensor.AddObservation(InstantaneousAcceleration);
        sensor.AddObservation(rBody.centerOfMass);
        sensor.AddObservation(target_location);
        sensor.AddObservation(transform.localPosition);

        if (InstantaneousAcceleration.magnitude < 0.001)
        {
            print($"Acceleration is 0 on step {this.StepCount}");
        }

    }

    public float forceMultiplier = 10;
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Actions, size = 4
        // Collect all of the control signals
        const float NUM_BINS = 9;

        float frontRightControlSignal = actionBuffers.DiscreteActions[0] / NUM_BINS;

        float frontLeftControlSignal = actionBuffers.DiscreteActions[1] / NUM_BINS;

        float backRightControlSignal = actionBuffers.DiscreteActions[2] / NUM_BINS;

        float backLeftControlSignal = actionBuffers.DiscreteActions[3] / NUM_BINS;

        // 1. Calculate normalized diagonal vectors for an X-configuration
        Vector3 frontRightDiagonal = (transform.forward + transform.right).normalized;
        Vector3 frontLeftDiagonal = (transform.forward - transform.right).normalized;
        Vector3 backRightDiagonal = (-transform.forward + transform.right).normalized;
        Vector3 backLeftDiagonal = (-transform.forward - transform.right).normalized;

        // 2. Create the 4 corner offsets for the forces
        Vector3 frontRightOffset = transform.position + transform.up + (frontRightDiagonal * pushDistance);
        Vector3 frontLeftOffset = transform.position + transform.up + (frontLeftDiagonal * pushDistance);
        Vector3 backRightOffset = transform.position + transform.up + (backRightDiagonal * pushDistance);
        Vector3 backLeftOffset = transform.position + transform.up + (backLeftDiagonal * pushDistance);

        Vector3 thrustDirection = transform.up;

        // 3. Apply forces at the 4 corner positions
        // Make sure your control signal variables match your input/action logic (e.g., FrontRight vs Front Left)
        rBody.AddForceAtPosition(frontRightControlSignal * forceMultiplier * thrustDirection, frontRightOffset, ForceMode.Force);
        rBody.AddForceAtPosition(frontLeftControlSignal * forceMultiplier * thrustDirection, frontLeftOffset, ForceMode.Force);
        rBody.AddForceAtPosition(backRightControlSignal * forceMultiplier * thrustDirection, backRightOffset, ForceMode.Force);
        rBody.AddForceAtPosition(backLeftControlSignal * forceMultiplier * thrustDirection, backLeftOffset, ForceMode.Force);




        // Rewards

        // Reached target
        SetReward(getReward());

        // Fell off platform
        if (this.IsOutOfBounds() || this.StepCount > this.maxStep)
        {
            EndEpisode();
        }
    }

    public bool IsOutOfBounds()
    {
        return (
            this.transform.localPosition.y > AREA_RADIUS || this.transform.localPosition.y < 8 ||
            this.transform.localPosition.x > AREA_RADIUS || this.transform.localPosition.x < -AREA_RADIUS ||
            this.transform.localPosition.z > AREA_RADIUS || this.transform.localPosition.z < -AREA_RADIUS
        );
    }

    void FixedUpdate()
    {
        // 1. Calculate the change in velocity since the last FixedUpdate step
        Vector3 currentVelocity = this.rBody.linearVelocity;

        // 2. Acceleration = Delta Velocity / Delta Time (m/s²)
        if (Time.fixedDeltaTime > 0)
        {
            InstantaneousAcceleration = (currentVelocity - previousVelocity) / Time.fixedDeltaTime;
        }

        // 3. Store current velocity for the next physics frame
        previousVelocity = currentVelocity;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Reference the discrete actions array out instead of continuous
        var discreteActionsOut = actionsOut.DiscreteActions;

        // Clear previous steps/defaults (index 0 usually means 0/9 or no thrust)
        discreteActionsOut[0] = 0; // Front Right
        discreteActionsOut[1] = 0; // Front Left
        discreteActionsOut[2] = 0; // Back Right
        discreteActionsOut[3] = 0; // Back Left

        if (Keyboard.current != null)
        {
            // Set your manual thrust level for when a key is held (e.g., 8 is max thrust)
            const int MAX_THRUST_INDEX = 8;

            // Map keys to specific corners based on your layout
            // W / Up Arrow -> Back Left & Back Right engines
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                discreteActionsOut[2] = MAX_THRUST_INDEX; // Back Right
                discreteActionsOut[3] = MAX_THRUST_INDEX; // Back Left
            }

            // S / Down Arrow -> Front Left & Front Right engines
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                discreteActionsOut[0] = MAX_THRUST_INDEX; // Front Right
                discreteActionsOut[1] = MAX_THRUST_INDEX; // Front Left
            }

            // A / Left Arrow -> Left side engines (Front Left, Back Left)
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                discreteActionsOut[1] = MAX_THRUST_INDEX; // Front Left
                discreteActionsOut[3] = MAX_THRUST_INDEX; // Back Left
            }

            // D / Right Arrow -> Right side engines (Front Right, Back Right)
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                discreteActionsOut[0] = MAX_THRUST_INDEX; // Front Right
                discreteActionsOut[2] = MAX_THRUST_INDEX; // Back Right
            }
        }
    }

}
