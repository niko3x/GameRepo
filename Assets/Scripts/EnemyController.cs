using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class OptimizedMLAgentsFightingController : Agent
{
    [Header("Combat Settings")]
    public float pushForce = 15f;
    public float pushRange = 2f;
    public float pushCooldown = 1f;
    private float lastPushTime;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float maxSpeed = 10f;
    public float rotationSpeed = 360f;
    public float accelerationForce = 15f;
    public float brakeForce = 10f;
    
    [Header("Jump Settings")]
    public float jumpForce = 10f;
    public LayerMask groundLayer = 1;
    
    [Header("Ground Check")]
    public float groundCheckDistance = 0.8f;
    public bool showGroundCheckDebug = false;
    
    [Header("Detection")]
    public float detectionRadius = 15f;
    public float healthThreshold = 50f;
    
    [Header("Rewards")]
    public float pushEnemyReward = 2f;
    public float knockOutEnemyReward = 10f;
    public float getHealthReward = 1f;
    public float fallOffPenalty = -10f;
    public float explorationReward = 0.2f;
    public float movementEfficiencyReward = 0.01f;
    public float survivalReward = 0.005f;
    public float stayInArenaReward = 0.002f;
    
    [Header("Episode Settings")]
    public float maxEpisodeTime = 300f;
    private float episodeStartTime;
    
    [Header("Performance Settings")]
    public float objectGatherInterval = 1f;
    public float explorationCheckInterval = 1f;
    public float minimumExplorationDistance = 3f;
    
    [Header("Decision System")]
    [Tooltip("Decisions requested per second (0 = every frame)")]
    public float decisionsPerSecond = 10f; // 10 Hz for good responsiveness
    [Tooltip("Enable frame-by-frame decision making")]
    public bool everyFrameDecisions = false;
    
    [Header("Play Area")]
    public Vector3 areaCenter = Vector3.zero;
    public Vector3 areaSize = new Vector3(20f, 10f, 20f);
    public bool endEpisodeOnLeaveArea = true;
    public float boundaryWarningDistance = 2f;
    
    [Header("Spawn Settings")]
    public float spawnHeightOffset = 1f;
    public float groundFindDistance = 30f;
    
    [Header("Debug")]
    public bool debugEpisodeEnding = true;
    public bool debugMovement = false;
    public bool debugRewards = false;
    [Tooltip("Show detailed episode reset reasons in console")]
    public bool verboseEpisodeDebug = true;
    [Tooltip("Track episode statistics over time")]
    public bool trackEpisodeStats = true;
    
    // Core Components
    private Rigidbody rb;
    private EnemyInventory enemyInventory;
    private Collider agentCollider;
    
    // State Variables
    private bool isGrounded;
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;
    private float currentSpeed;
    
    // ML-Agents Actions (cached for performance)
    private Vector2 moveInput;
    private bool jumpInput;
    private bool pushInput;
    private Vector2 lastMoveInput;
    
    // Object Collections (optimized)
    private List<HealthItem> healthItems = new List<HealthItem>(10);
    private List<OptimizedMLAgentsFightingController> otherEnemies = new List<OptimizedMLAgentsFightingController>(10);
    
    // Performance Caches
    private HealthItem cachedClosestHealthItem;
    private OptimizedMLAgentsFightingController cachedClosestEnemy;
    private float lastCacheUpdateTime;
    private const float CACHE_UPDATE_INTERVAL = 0.05f; // 20 Hz cache updates
    
    // Decision System Variables
    private float lastDecisionTime;
    private float decisionInterval;
    private bool hasValidDecision;
    
    // Performance optimization
    private float lastGatherTime = -10f;
    private float lastExplorationTime;
    private Vector3 lastExplorationPosition;
    private readonly RaycastHit[] groundHits = new RaycastHit[5];
    private readonly Collider[] nearbyColliders = new Collider[20];
    
    // Movement optimization
    private Vector3 smoothedInput;
    private readonly float inputSmoothTime = 0.1f;
    private Vector3 inputVelocity;
    
    // Episode debugging and statistics
    public enum EpisodeEndReason
    {
        Unknown,
        LeftArena,
        FellTooLow,
        Timeout,
        HealthDepleted,
        ManualReset,
        ExternalForce
    }
    
    [System.Serializable]
    public class EpisodeStats
    {
        public int episodeNumber;
        public float duration;
        public float finalReward;
        public Vector3 startPosition;
        public Vector3 endPosition;
        public EpisodeEndReason endReason;
        public string reasonDetails;
        public float maxDistanceFromCenter;
        public int pushesExecuted;
        public int jumpsExecuted;
        public float timeOutsideArena;
        public float averageSpeed;
        public System.DateTime timestamp;
    }
    
    private EpisodeStats currentEpisodeStats;
    private List<EpisodeStats> episodeHistory = new List<EpisodeStats>();
    private int totalEpisodes = 0;
    private float timeOutsideArena = 0f;
    private bool wasOutsideArena = false;
    private int pushCount = 0;
    private int jumpCount = 0;
    private float maxDistanceFromCenter = 0f;
    private float speedSum = 0f;
    private int speedSamples = 0;

    public override void Initialize()
    {
        InitializeComponents();
        ConfigureRigidbody();
        InitializeDecisionSystem();
        
        if (debugMovement)
            Debug.Log($"[{gameObject.name}] Agent initialized successfully");
    }
    
    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        agentCollider = GetComponent<Collider>();
        if (agentCollider == null)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = new Vector3(0, 1f, 0);
            agentCollider = capsule;
        }

        enemyInventory = GetComponent<EnemyInventory>();
        if (enemyInventory == null)
        {
            Debug.LogWarning($"[{gameObject.name}] No EnemyInventory component found!");
        }
    }

    private void ConfigureRigidbody()
    {
        rb.mass = 1f;
        rb.linearDamping = 2f; // Using the newer linearDamping property
        rb.angularDamping = 10f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Smoother movement
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }
    
    private void InitializeDecisionSystem()
    {
        // Remove any existing DecisionRequester for manual control
        var existingRequester = GetComponent<Unity.MLAgents.DecisionRequester>();
        if (existingRequester != null)
        {
            if (Application.isPlaying)
                Destroy(existingRequester);
            else
                DestroyImmediate(existingRequester);
        }
        
        // Calculate decision interval
        if (everyFrameDecisions)
        {
            decisionInterval = 0f; // Every frame
        }
        else if (decisionsPerSecond > 0)
        {
            decisionInterval = 1f / decisionsPerSecond;
        }
        else
        {
            decisionInterval = 0.1f; // Default 10 Hz
        }
        
        lastDecisionTime = 0f;
        hasValidDecision = false;
        
        if (debugMovement)
        {
            Debug.Log($"[{gameObject.name}] Decision system: {(everyFrameDecisions ? "Every Frame" : $"{decisionsPerSecond} Hz")}");
        }
    }
    
    void Start()
    {
        lastExplorationPosition = transform.position;
        lastExplorationTime = Time.time;
        
        // Initial object gathering
        GatherGameObjects();
        
        LogAgentSetup();
    }
    
    private void LogAgentSetup()
    {
        var behaviorParams = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if (behaviorParams != null)
        {
            Debug.Log($"[{gameObject.name}] ML-Agents Setup - Behavior: {behaviorParams.BehaviorType}, " +
                     $"Vector Obs: {behaviorParams.BrainParameters.VectorObservationSize}, " +
                     $"Continuous: {behaviorParams.BrainParameters.ActionSpec.NumContinuousActions}, " +
                     $"Discrete: {behaviorParams.BrainParameters.ActionSpec.NumDiscreteActions}");
        }
    }

    public override void OnEpisodeBegin()
    {
        // Start new episode statistics tracking
        StartNewEpisodeStats();
        
        if (debugEpisodeEnding)
            Debug.Log($"[{gameObject.name}] Episode #{totalEpisodes} Beginning");
        
        episodeStartTime = Time.time;
        
        // Reset to random position
        Vector3 newPosition = GetRandomArenaPosition();
        ResetAgentTransform(newPosition);
        
        // Store start position for stats
        currentEpisodeStats.startPosition = newPosition;
        
        // Reset all state
        ResetAgentState();
        
        // Force immediate cache update
        InvalidateCache();
        GatherGameObjects();
        
        if (verboseEpisodeDebug)
        {
            Debug.Log($"<color=green>[{gameObject.name}] Episode #{totalEpisodes} STARTED</color>\n" +
                     $"Start Position: {newPosition}\n" +
                     $"Arena Center: {areaCenter}\n" +
                     $"Arena Size: {areaSize}\n" +
                     $"Max Episode Time: {maxEpisodeTime}s");
        }
    }
    
    private void StartNewEpisodeStats()
    {
        totalEpisodes++;
        
        currentEpisodeStats = new EpisodeStats
        {
            episodeNumber = totalEpisodes,
            startPosition = transform.position,
            endReason = EpisodeEndReason.Unknown,
            reasonDetails = "",
            timestamp = System.DateTime.Now,
            pushesExecuted = 0,
            jumpsExecuted = 0,
            timeOutsideArena = 0f,
            maxDistanceFromCenter = 0f,
            averageSpeed = 0f
        };
        
        // Reset episode counters
        timeOutsideArena = 0f;
        wasOutsideArena = false;
        pushCount = 0;
        jumpCount = 0;
        maxDistanceFromCenter = 0f;
        speedSum = 0f;
        speedSamples = 0;
    }
    
    private Vector3 GetRandomArenaPosition()
    {
        // Generate random position within 80% of arena bounds (safety margin)
        float margin = 0.8f;
        float randomX = Random.Range(-areaSize.x * margin * 0.5f, areaSize.x * margin * 0.5f);
        float randomZ = Random.Range(-areaSize.z * margin * 0.5f, areaSize.z * margin * 0.5f);
        
        Vector3 randomPosition = areaCenter + new Vector3(randomX, areaSize.y * 0.5f, randomZ);
        
        // Find ground
        Vector3 groundPosition = FindGroundPosition(randomPosition);
        
        return groundPosition;
    }
    
    private Vector3 FindGroundPosition(Vector3 startPosition)
    {
        if (Physics.Raycast(startPosition, Vector3.down, out RaycastHit hit, groundFindDistance, groundLayer))
        {
            return hit.point + Vector3.up * spawnHeightOffset;
        }
        
        // Fallback: use area center at ground level
        return areaCenter + Vector3.up * spawnHeightOffset;
    }

    private void ResetAgentTransform(Vector3 newPosition)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        transform.position = newPosition;
        transform.rotation = Quaternion.identity;
        
        // Reset movement state
        currentVelocity = Vector3.zero;
        targetVelocity = Vector3.zero;
        smoothedInput = Vector3.zero;
        inputVelocity = Vector3.zero;
    }
    
    private void ResetAgentState()
    {
        if (enemyInventory != null)
        {
            enemyInventory.health = 100;
        }
        
        lastPushTime = 0f;
        lastExplorationTime = Time.time;
        lastExplorationPosition = transform.position;
        lastDecisionTime = 0f;
        hasValidDecision = false;
        
        // Reset input states
        moveInput = Vector2.zero;
        lastMoveInput = Vector2.zero;
        jumpInput = false;
        pushInput = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        UpdateObjectCache();
        
        // Normalize position relative to arena (3 observations)
        Vector3 normalizedPos = GetNormalizedArenaPosition();
        sensor.AddObservation(normalizedPos);
        
        // Velocity information (3 observations)
        Vector3 normalizedVel = rb.linearVelocity / maxSpeed;
        sensor.AddObservation(normalizedVel);
        
        // Health and combat state (4 observations)
        float healthRatio = enemyInventory != null ? Mathf.Clamp01(enemyInventory.health / 100f) : 1f;
        sensor.AddObservation(healthRatio);
        sensor.AddObservation(healthRatio < (healthThreshold / 100f) ? 1f : 0f);
        sensor.AddObservation(isGrounded ? 1f : 0f);
        sensor.AddObservation(CanPush() ? 1f : 0f);
        
        // Arena boundary information (4 observations)
        Vector4 boundaryInfo = GetBoundaryInfo();
        sensor.AddObservation(boundaryInfo.x);
        sensor.AddObservation(boundaryInfo.y);
        sensor.AddObservation(boundaryInfo.z);
        sensor.AddObservation(boundaryInfo.w);
        
        // Closest health item (4 observations)
        Vector4 healthInfo = GetHealthItemInfo();
        sensor.AddObservation(healthInfo.x);
        sensor.AddObservation(healthInfo.y);
        sensor.AddObservation(healthInfo.z);
        sensor.AddObservation(healthInfo.w);
        
        // Closest enemy (5 observations)
        Vector4 enemyDirection = GetEnemyInfo();
        sensor.AddObservation(enemyDirection.x);
        sensor.AddObservation(enemyDirection.y);
        sensor.AddObservation(enemyDirection.z);
        sensor.AddObservation(enemyDirection.w);
        sensor.AddObservation(cachedClosestEnemy != null ? 1f : 0f); // Enemy exists flag
        
        // Movement efficiency (2 observations)
        sensor.AddObservation(currentSpeed / maxSpeed);
        sensor.AddObservation(Vector3.Dot(rb.linearVelocity.normalized, transform.forward));
        
        // Total: 30 observations (well-structured and normalized)
    }
    
    private Vector3 GetNormalizedArenaPosition()
    {
        Vector3 localPos = transform.position - areaCenter;
        return new Vector3(
            localPos.x / (areaSize.x * 0.5f),
            Mathf.Clamp(localPos.y / (areaSize.y * 0.5f), -1f, 1f),
            localPos.z / (areaSize.z * 0.5f)
        );
    }
    
    private Vector4 GetBoundaryInfo()
    {
        Vector3 localPos = transform.position - areaCenter;
        Vector3 halfSize = areaSize * 0.5f;
        
        float distToXBound = Mathf.Min(halfSize.x - Mathf.Abs(localPos.x), halfSize.x) / halfSize.x;
        float distToZBound = Mathf.Min(halfSize.z - Mathf.Abs(localPos.z), halfSize.z) / halfSize.z;
        float distToYBound = (transform.position.y - areaCenter.y + halfSize.y) / areaSize.y;
        
        return new Vector4(distToXBound, distToZBound, distToYBound, IsInPlayArea() ? 1f : 0f);
    }
    
    private Vector4 GetHealthItemInfo()
    {
        if (cachedClosestHealthItem != null)
        {
            Vector3 toHealth = cachedClosestHealthItem.transform.position - transform.position;
            float distance = toHealth.magnitude;
            Vector3 direction = toHealth / distance; // Normalize
            float normalizedDistance = Mathf.Clamp01(distance / detectionRadius);
            
            return new Vector4(direction.x, direction.y, direction.z, 1f - normalizedDistance);
        }
        return Vector4.zero;
    }
    
    private Vector4 GetEnemyInfo()
    {
        if (cachedClosestEnemy != null)
        {
            Vector3 toEnemy = cachedClosestEnemy.transform.position - transform.position;
            float distance = toEnemy.magnitude;
            Vector3 direction = toEnemy / distance; // Normalize
            float normalizedDistance = Mathf.Clamp01(distance / detectionRadius);
            
            return new Vector4(direction.x, direction.y, direction.z, 1f - normalizedDistance);
        }
        return Vector4.zero;
    }
    
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        ExtractActions(actionBuffers);
        hasValidDecision = true;
    }
    
    private void ExtractActions(ActionBuffers actionBuffers)
    {
        // Store previous input for smoothing
        lastMoveInput = moveInput;
        
        // Extract and clamp continuous actions
        moveInput.x = Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, 1f);
        moveInput.y = Mathf.Clamp(actionBuffers.ContinuousActions[1], -1f, 1f);
        
        // Extract discrete actions
        jumpInput = actionBuffers.DiscreteActions[0] == 1;
        pushInput = actionBuffers.DiscreteActions[1] == 1;
    }

    [System.Obsolete]
    void FixedUpdate()
    {
        // Update ground state
        CheckGrounded();
        
        // Handle decision timing
        HandleDecisionTiming();
        
        // Execute actions (only if we have valid decisions)
        if (hasValidDecision)
        {
            HandleOptimizedMovement();
            HandleCombat();
        }
        
        // Update current speed for observations
        currentSpeed = rb.linearVelocity.magnitude;
        
        // Calculate rewards
        CalculateRewards();
        
        // Check episode end conditions
        CheckEpisodeEndConditions();
    }
    
    private void HandleDecisionTiming()
    {
        bool shouldRequestDecision = false;
        
        if (everyFrameDecisions)
        {
            shouldRequestDecision = true;
        }
        else if (Time.time - lastDecisionTime >= decisionInterval)
        {
            shouldRequestDecision = true;
            lastDecisionTime = Time.time;
        }
        
        if (shouldRequestDecision)
        {
            RequestDecision();
        }
        else
        {
            // Continue with last action if we have one
            RequestAction();
        }
    }

    [System.Obsolete]
    private void HandleOptimizedMovement()
    {
        if (rb == null) return;
        
        // Smooth input for better control
        Vector3 targetInput = new Vector3(moveInput.x, 0, moveInput.y);
        smoothedInput = Vector3.SmoothDamp(smoothedInput, targetInput, ref inputVelocity, inputSmoothTime);
        
        // Calculate target velocity
        targetVelocity = smoothedInput * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y; // Preserve vertical velocity
        
        // Apply force based on difference between current and target velocity
        Vector3 velocityDifference = targetVelocity - rb.linearVelocity;
        velocityDifference.y = 0; // Don't interfere with gravity/jumping
        
        // Dynamic force application
        float forceMultiplier = Mathf.Lerp(accelerationForce, brakeForce, 
            Vector3.Dot(rb.linearVelocity.normalized, targetVelocity.normalized));
        
        Vector3 force = velocityDifference * forceMultiplier;
        
        // Apply speed limit
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            Vector3 limitedVelocity = rb.linearVelocity.normalized * maxSpeed;
            limitedVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = limitedVelocity;
        }
        else
        {
            rb.AddForce(force, ForceMode.Force);
        }
        
        // Handle jumping
        if (jumpInput && isGrounded && rb.linearVelocity.y < 1f)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            
            if (debugMovement)
                Debug.Log($"[{gameObject.name}] Jump executed");
        }
        
        // Optional rotation towards movement direction
        if (smoothedInput.magnitude > 0.1f)
        {
            Vector3 lookDirection = new Vector3(smoothedInput.x, 0, smoothedInput.z);
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 
                rotationSpeed * Time.fixedDeltaTime);
        }
    }
    
    private void HandleCombat()
    {
        if (pushInput && CanPush())
        {
            PerformPushAttack();
            lastPushTime = Time.time;
        }
    }
    
    private void PerformPushAttack()
    {
        int pushedEnemies = 0;
        pushCount++; // Track pushes for statistics
        
        foreach (var enemy in otherEnemies)
        {
            if (enemy == null || enemy == this) continue;
            
            float sqrDistance = (transform.position - enemy.transform.position).sqrMagnitude;
            if (sqrDistance <= pushRange * pushRange)
            {
                Vector3 pushDirection = (enemy.transform.position - transform.position).normalized;
                pushDirection.y = 0.2f; // Slight upward component
                
                if (enemy.rb != null)
                {
                    enemy.rb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
                    pushedEnemies++;
                    
                    if (debugMovement)
                        Debug.Log($"[{gameObject.name}] Push #{pushCount} - Pushed {enemy.gameObject.name}!");
                }
            }
        }
        
        if (pushedEnemies > 0)
        {
            AddReward(pushEnemyReward * pushedEnemies);
        }
    }
    
    void Update()
    {
        // Periodic object gathering (less frequent than FixedUpdate)
        GatherGameObjects();
    }
    
    private void CheckGrounded()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, groundHits, groundCheckDistance, groundLayer);
        
        bool wasGrounded = isGrounded;
        isGrounded = hitCount > 0;
        
        // Debug ground state changes
        if (debugMovement && wasGrounded != isGrounded)
        {
            Debug.Log($"[{gameObject.name}] Ground state changed: {wasGrounded} -> {isGrounded}");
        }
        
        if (showGroundCheckDebug)
        {
            Debug.DrawRay(rayStart, Vector3.down * groundCheckDistance, 
                isGrounded ? Color.green : Color.red, 0.1f);
        }
    }

    [System.Obsolete]
    private void CalculateRewards()
    {
        float frameReward = 0f;
        
        // Basic survival reward
        frameReward += survivalReward;
        
        // Reward for staying in arena
        if (IsInPlayArea())
        {
            frameReward += stayInArenaReward;
            
            // Bonus for staying away from boundaries
            float boundaryDistance = GetDistanceToBoundary();
            if (boundaryDistance > boundaryWarningDistance)
            {
                frameReward += stayInArenaReward * 0.5f;
            }
        }
        else
        {
            // Penalty for being outside arena
            frameReward -= 0.02f;
        }
        
        // Movement efficiency reward
        if (moveInput.magnitude > 0.1f)
        {
            float efficiency = Vector3.Dot(rb.velocity.normalized, transform.forward);
            frameReward += movementEfficiencyReward * efficiency;
        }
        
        // Reward for moving towards enemies
        if (cachedClosestEnemy != null)
        {
            Vector3 toEnemy = cachedClosestEnemy.transform.position - transform.position;
            float enemyDistance = toEnemy.magnitude;
            
            if (enemyDistance < detectionRadius)
            {
                // Reward for being close to enemies
                float proximityReward = (detectionRadius - enemyDistance) / detectionRadius * 0.01f;
                frameReward += proximityReward;
                
                // Extra reward for moving towards enemy
                if (rb.linearVelocity.magnitude > 1f)
                {
                    float alignment = Vector3.Dot(rb.linearVelocity.normalized, toEnemy.normalized);
                    if (alignment > 0.5f)
                    {
                        frameReward += movementEfficiencyReward * alignment;
                    }
                }
            }
        }
        
        // Exploration reward (less frequent checks)
        CheckExplorationReward();
        
        // Small penalty to encourage efficiency
        frameReward -= 0.0005f;
        
        AddReward(frameReward);
        
        if (debugRewards && Time.time % 2f < Time.fixedDeltaTime)
        {
            Debug.Log($"[{gameObject.name}] Frame reward: {frameReward:F4}, Total: {GetCumulativeReward():F2}");
        }
    }
    
    private void CheckExplorationReward()
    {
        if (Time.time - lastExplorationTime > explorationCheckInterval)
        {
            float explorationDistance = Vector3.Distance(transform.position, lastExplorationPosition);
            
            if (explorationDistance > minimumExplorationDistance)
            {
                float reward = explorationReward * (explorationDistance / minimumExplorationDistance);
                AddReward(reward);
                
                lastExplorationTime = Time.time;
                lastExplorationPosition = transform.position;
                
                if (debugRewards)
                    Debug.Log($"[{gameObject.name}] Exploration reward: {reward:F3}");
            }
        }
    }
    
    private void CheckEpisodeEndConditions()
    {
        // Check arena bounds
        if (endEpisodeOnLeaveArea && !IsInPlayArea())
        {
            EndEpisodeWithReason(EpisodeEndReason.LeftArena, 
                $"Agent left play area at position {transform.position:F1}. " +
                $"Arena center: {areaCenter}, Arena size: {areaSize}. " +
                $"Distance from center: {Vector3.Distance(transform.position, areaCenter):F1}");
            return;
        }
        
        // Check if fallen too far
        float minY = areaCenter.y - areaSize.y;
        if (transform.position.y < minY)
        {
            EndEpisodeWithReason(EpisodeEndReason.FellTooLow, 
                $"Agent fell below minimum Y ({minY:F1}) at position {transform.position:F1}. " +
                $"Fall distance: {minY - transform.position.y:F1}");
            return;
        }
        
        // Check health depletion
        if (enemyInventory != null && enemyInventory.health <= 0)
        {
            EndEpisodeWithReason(EpisodeEndReason.HealthDepleted, 
                $"Agent health depleted (Health: {enemyInventory.health:F1})");
            return;
        }
        
        // Check episode timeout
        float episodeDuration = Time.time - episodeStartTime;
        if (episodeDuration > maxEpisodeTime)
        {
            EndEpisodeWithReason(EpisodeEndReason.Timeout, 
                $"Episode reached maximum duration ({maxEpisodeTime}s). " +
                $"Final position: {transform.position:F1}");
            return;
        }
    }
    
    private void EndEpisodeWithReason(EpisodeEndReason reason, string details)
    {
        // Complete episode statistics
        CompleteEpisodeStats(reason, details);
        
        // Log the episode end reason
        if (debugEpisodeEnding || verboseEpisodeDebug)
        {
            string colorCode = GetReasonColorCode(reason);
            Debug.Log($"<color={colorCode}>[{gameObject.name}] Episode #{totalEpisodes} ENDED</color>\n" +
                     $"<b>Reason:</b> {reason}\n" +
                     $"<b>Details:</b> {details}\n" +
                     $"<b>Duration:</b> {currentEpisodeStats.duration:F1}s\n" +
                     $"<b>Final Reward:</b> {GetCumulativeReward():F2}\n" +
                     $"<b>Start→End:</b> {currentEpisodeStats.startPosition:F1} → {transform.position:F1}\n" +
                     $"<b>Stats:</b> Pushes: {pushCount}, Jumps: {jumpCount}, Time Outside: {timeOutsideArena:F1}s\n" +
                     $"<b>Max Distance from Center:</b> {maxDistanceFromCenter:F1}\n" +
                     $"<b>Average Speed:</b> {currentEpisodeStats.averageSpeed:F1}");
        }
        
        // Apply penalty reward based on reason
        ApplyEndReasonPenalty(reason);
        
        // Store episode in history
        if (trackEpisodeStats && episodeHistory.Count < 1000) // Limit history size
        {
            episodeHistory.Add(currentEpisodeStats);
        }
        
        // End the episode
        EndEpisode();
    }
    
    private void CompleteEpisodeStats(EpisodeEndReason reason, string details)
    {
        currentEpisodeStats.duration = Time.time - episodeStartTime;
        currentEpisodeStats.finalReward = GetCumulativeReward();
        currentEpisodeStats.endPosition = transform.position;
        currentEpisodeStats.endReason = reason;
        currentEpisodeStats.reasonDetails = details;
        currentEpisodeStats.pushesExecuted = pushCount;
        currentEpisodeStats.jumpsExecuted = jumpCount;
        currentEpisodeStats.timeOutsideArena = timeOutsideArena;
        currentEpisodeStats.maxDistanceFromCenter = maxDistanceFromCenter;
        currentEpisodeStats.averageSpeed = speedSamples > 0 ? speedSum / speedSamples : 0f;
    }
    
    private string GetReasonColorCode(EpisodeEndReason reason)
    {
        switch (reason)
        {
            case EpisodeEndReason.Timeout: return "blue";
            case EpisodeEndReason.LeftArena: return "red";
            case EpisodeEndReason.FellTooLow: return "orange";
            case EpisodeEndReason.HealthDepleted: return "purple";
            case EpisodeEndReason.ManualReset: return "yellow";
            case EpisodeEndReason.ExternalForce: return "cyan";
            default: return "white";
        }
    }
    
    private void ApplyEndReasonPenalty(EpisodeEndReason reason)
    {
        switch (reason)
        {
            case EpisodeEndReason.LeftArena:
            case EpisodeEndReason.FellTooLow:
                AddReward(fallOffPenalty);
                break;
            case EpisodeEndReason.HealthDepleted:
                AddReward(fallOffPenalty * 0.5f); // Smaller penalty for health depletion
                break;
            case EpisodeEndReason.Timeout:
                // No penalty for timeout - agent survived!
                AddReward(1f); // Small bonus for lasting the full episode
                break;
        }
    }
    
    private void UpdateEpisodeStats()
    {
        // Track distance from arena center
        float distanceFromCenter = Vector3.Distance(transform.position, areaCenter);
        if (distanceFromCenter > maxDistanceFromCenter)
        {
            maxDistanceFromCenter = distanceFromCenter;
        }
        
        // Track time outside arena
        bool currentlyOutside = !IsInPlayArea();
        if (currentlyOutside && !wasOutsideArena)
        {
            // Just went outside
            wasOutsideArena = true;
        }
        else if (!currentlyOutside && wasOutsideArena)
        {
            // Just came back inside
            wasOutsideArena = false;
        }
        
        if (wasOutsideArena)
        {
            timeOutsideArena += Time.fixedDeltaTime;
        }
        
        // Track average speed
        if (currentSpeed > 0.1f) // Only count when actually moving
        {
            speedSum += currentSpeed;
            speedSamples++;
        }
    }
    
    private void GatherGameObjects()
    {
        if (Time.time - lastGatherTime < objectGatherInterval) return;
        lastGatherTime = Time.time;
        
        UpdateHealthItems();
        UpdateEnemies();
        InvalidateCache();
    }
    
    private void UpdateHealthItems()
    {
        healthItems.Clear();
        
        // Use OverlapSphere for more efficient nearby object detection
        int colliderCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, 
            nearbyColliders, -1, QueryTriggerInteraction.Collide);
        
        for (int i = 0; i < colliderCount; i++)
        {
            if (nearbyColliders[i].CompareTag("Item"))
            {
                var healthItem = nearbyColliders[i].GetComponent<HealthItem>();
                if (healthItem != null)
                {
                    healthItems.Add(healthItem);
                }
            }
        }
    }
    
    private void UpdateEnemies()
    {
        otherEnemies.Clear();
        
        // Find all agents of this type
        var allAgents = FindObjectsByType<OptimizedMLAgentsFightingController>(FindObjectsSortMode.None);
        
        foreach (var agent in allAgents)
        {
            if (agent != this && agent != null && agent.gameObject.activeInHierarchy)
            {
                float sqrDistance = (transform.position - agent.transform.position).sqrMagnitude;
                if (sqrDistance <= detectionRadius * detectionRadius)
                {
                    otherEnemies.Add(agent);
                }
            }
        }
    }
    
    private void UpdateObjectCache()
    {
        if (Time.time - lastCacheUpdateTime < CACHE_UPDATE_INTERVAL) return;
        
        cachedClosestHealthItem = FindClosestHealthItem();
        cachedClosestEnemy = FindClosestEnemy();
        lastCacheUpdateTime = Time.time;
    }
    
    private void InvalidateCache()
    {
        lastCacheUpdateTime = 0f;
    }
    
    private HealthItem FindClosestHealthItem()
    {
        if (healthItems.Count == 0) return null;
        
        HealthItem closest = null;
        float closestSqrDistance = Mathf.Infinity;
        
        foreach (var item in healthItems)
        {
            if (item == null) continue;
            
            float sqrDistance = (transform.position - item.transform.position).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = item;
            }
        }
        
        return closest;
    }
    
    private OptimizedMLAgentsFightingController FindClosestEnemy()
    {
        if (otherEnemies.Count == 0) return null;
        
        OptimizedMLAgentsFightingController closest = null;
        float closestSqrDistance = Mathf.Infinity;
        
        foreach (var enemy in otherEnemies)
        {
            if (enemy == null) continue;
            
            float sqrDistance = (transform.position - enemy.transform.position).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = enemy;
            }
        }
        
        return closest;
    }
    
    private bool CanPush()
    {
        return Time.time - lastPushTime > pushCooldown;
    }
    
    private bool IsInPlayArea()
    {
        Vector3 localPos = transform.position - areaCenter;
        Vector3 halfSize = areaSize * 0.5f;
        
        return Mathf.Abs(localPos.x) <= halfSize.x &&
               localPos.y >= -halfSize.y && localPos.y <= halfSize.y &&
               Mathf.Abs(localPos.z) <= halfSize.z;
    }
    
    private float GetDistanceToBoundary()
    {
        Vector3 localPos = transform.position - areaCenter;
        Vector3 halfSize = areaSize * 0.5f;
        
        float distToXBound = halfSize.x - Mathf.Abs(localPos.x);
        float distToZBound = halfSize.z - Mathf.Abs(localPos.z);
        
        return Mathf.Min(distToXBound, distToZBound);
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual keyboard control for testing
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;
        
        // Smooth input handling
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        continuousActionsOut[0] = horizontal;
        continuousActionsOut[1] = vertical;
        
        discreteActionsOut[0] = Input.GetKey(KeyCode.Space) ? 1 : 0; // Jump
        discreteActionsOut[1] = Input.GetKey(KeyCode.F) ? 1 : 0;     // Push
        
        // Debug input
        if (debugMovement && (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f))
        {
            Debug.Log($"[{gameObject.name}] Heuristic input: ({horizontal:F2}, {vertical:F2})");
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Handle health item collection
        if (other.CompareTag("Item"))
        {
            var healthItem = other.GetComponent<HealthItem>();
            if (healthItem != null)
            {
                AddReward(getHealthReward);
                
                if (debugRewards)
                    Debug.Log($"[{gameObject.name}] Health item collected! Reward: {getHealthReward}");
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Handle enemy collisions
        if (collision.gameObject.TryGetComponent<OptimizedMLAgentsFightingController>(out var otherAgent))
        {
            // Small reward for making contact with enemies
            AddReward(0.1f);
            
            if (debugMovement)
                Debug.Log($"[{gameObject.name}] Collision with {otherAgent.gameObject.name}");
        }
    }
    
    // Public methods for external systems
    public float GetCurrentHealth()
    {
        return enemyInventory != null ? enemyInventory.health : 100f;
    }
    
    public bool IsAlive()
    {
        return GetCurrentHealth() > 0f;
    }

    [System.Obsolete]
    public Vector3 GetVelocity()
    {
        return rb != null ? rb.linearVelocity : Vector3.zero;
    }
    
    public float GetCurrentSpeed()
    {
        return currentSpeed;
    }
    
    public bool IsInArena()
    {
        return IsInPlayArea();
    }

    // Debug and utility methods
    [System.Obsolete]
    void OnDrawGizmosSelected()
    {
        // Draw push range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, pushRange);
        
        // Draw detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Draw play area bounds
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(areaCenter, areaSize);
        
        // Draw boundary warning zone
        Vector3 warningSize = areaSize - Vector3.one * (boundaryWarningDistance * 2f);
        Gizmos.color = Color.orange;
        Gizmos.DrawWireCube(areaCenter, warningSize);
        
        // Draw line to area center
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, areaCenter);
        
        // Draw ground check ray
        if (showGroundCheckDebug)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * groundCheckDistance);
        }
        
        // Draw velocity vector
        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
            
            // Draw target velocity
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, targetVelocity);
        }
        
        // Draw connections to nearby objects
        if (Application.isPlaying)
        {
            // Line to closest health item
            if (cachedClosestHealthItem != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, cachedClosestHealthItem.transform.position);
            }
            
            // Line to closest enemy
            if (cachedClosestEnemy != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, cachedClosestEnemy.transform.position);
            }
        }
    }
    
    [ContextMenu("Debug Agent Status")]
    [System.Obsolete]
    public void DebugAgentStatus()
    {
        Debug.Log("=== AGENT STATUS DEBUG ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"Position: {transform.position}");
        Debug.Log($"In Play Area: {IsInPlayArea()}");
        Debug.Log($"Distance to Boundary: {GetDistanceToBoundary():F2}");
        Debug.Log($"Health: {GetCurrentHealth():F1}");
        Debug.Log($"Is Grounded: {isGrounded}");
        Debug.Log($"Current Speed: {currentSpeed:F2}");
        Debug.Log($"Velocity: {rb.linearVelocity}");
        Debug.Log($"Can Push: {CanPush()}");
        
        Debug.Log($"=== DECISION SYSTEM ===");
        Debug.Log($"Every Frame Decisions: {everyFrameDecisions}");
        Debug.Log($"Decisions Per Second: {decisionsPerSecond}");
        Debug.Log($"Decision Interval: {decisionInterval:F3}s");
        Debug.Log($"Has Valid Decision: {hasValidDecision}");
        Debug.Log($"Last Decision Time: {Time.time - lastDecisionTime:F3}s ago");
        
        Debug.Log($"=== CACHED OBJECTS ===");
        Debug.Log($"Health Items Found: {healthItems.Count}");
        Debug.Log($"Enemies Found: {otherEnemies.Count}");
        Debug.Log($"Closest Health Item: {(cachedClosestHealthItem != null ? cachedClosestHealthItem.name : "None")}");
        Debug.Log($"Closest Enemy: {(cachedClosestEnemy != null ? cachedClosestEnemy.name : "None")}");
        
        Debug.Log($"=== EPISODE INFO ===");
        Debug.Log($"Episode Time: {Time.time - episodeStartTime:F1}s / {maxEpisodeTime}s");
        Debug.Log($"Cumulative Reward: {GetCumulativeReward():F2}");
        
        // Test components
        var behaviorParams = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        Debug.Log($"Behavior Parameters: {(behaviorParams != null ? "Found" : "Missing")}");
        Debug.Log($"Rigidbody: {(rb != null ? "Found" : "Missing")}");
        Debug.Log($"Enemy Inventory: {(enemyInventory != null ? "Found" : "Missing")}");
        Debug.Log($"Collider: {(agentCollider != null ? "Found" : "Missing")}");
    }
    
    [ContextMenu("Force Decision Request")]
    public void ForceDecisionRequest()
    {
        Debug.Log($"[{gameObject.name}] Forcing decision request...");
        RequestDecision();
        hasValidDecision = true;
    }
    
    [ContextMenu("Reset Episode")]
    public void ForceEpisodeReset()
    {
        Debug.Log($"[{gameObject.name}] Forcing episode reset...");
        EndEpisodeWithReason(EpisodeEndReason.ManualReset, "Episode manually reset via context menu");
    }
    
    [ContextMenu("Show Episode Statistics")]
    public void ShowEpisodeStatistics()
    {
        if (!trackEpisodeStats || episodeHistory.Count == 0)
        {
            Debug.Log($"[{gameObject.name}] No episode statistics available. Enable 'Track Episode Stats' to collect data.");
            return;
        }
        
        // Calculate statistics
        int totalEps = episodeHistory.Count;
        var reasons = System.Enum.GetValues(typeof(EpisodeEndReason));
        
        Debug.Log($"=== EPISODE STATISTICS FOR {gameObject.name} ===");
        Debug.Log($"Total Episodes Completed: {totalEps}");
        Debug.Log($"Current Episode: #{totalEpisodes}");
        
        // Breakdown by end reason
        Debug.Log("\n<b>Episode End Reasons:</b>");
        foreach (EpisodeEndReason reason in reasons)
        {
            int count = episodeHistory.Count(ep => ep.endReason == reason);
            if (count > 0)
            {
                float percentage = (count / (float)totalEps) * 100f;
                Debug.Log($"  {reason}: {count} ({percentage:F1}%)");
            }
        }
        
        // Duration statistics
        var durations = episodeHistory.Select(ep => ep.duration).ToArray();
        Debug.Log($"\n<b>Episode Durations:</b>");
        Debug.Log($"  Average: {durations.Average():F1}s");
        Debug.Log($"  Min: {durations.Min():F1}s");
        Debug.Log($"  Max: {durations.Max():F1}s");
        
        // Reward statistics
        var rewards = episodeHistory.Select(ep => ep.finalReward).ToArray();
        Debug.Log($"\n<b>Final Rewards:</b>");
        Debug.Log($"  Average: {rewards.Average():F2}");
        Debug.Log($"  Min: {rewards.Min():F2}");
        Debug.Log($"  Max: {rewards.Max():F2}");
        
        // Recent performance (last 10 episodes)
        var recent = episodeHistory.TakeLast(Mathf.Min(10, totalEps)).ToArray();
        Debug.Log($"\n<b>Recent Performance (last {recent.Length} episodes):</b>");
        Debug.Log($"  Average Duration: {recent.Select(ep => ep.duration).Average():F1}s");
        Debug.Log($"  Average Reward: {recent.Select(ep => ep.finalReward).Average():F2}");
        Debug.Log($"  Most Common End Reason: {recent.GroupBy(ep => ep.endReason).OrderByDescending(g => g.Count()).First().Key}");
        
        // Performance trends
        if (totalEps >= 20)
        {
            var first10 = episodeHistory.Take(10).ToArray();
            var last10 = episodeHistory.TakeLast(10).ToArray();
            
            float durationImprovement = last10.Select(ep => ep.duration).Average() - first10.Select(ep => ep.duration).Average();
            float rewardImprovement = last10.Select(ep => ep.finalReward).Average() - first10.Select(ep => ep.finalReward).Average();
            
            Debug.Log($"\n<b>Improvement Trends (first 10 vs last 10):</b>");
            Debug.Log($"  Duration Change: {durationImprovement:+F1;-F1;0}s");
            Debug.Log($"  Reward Change: {rewardImprovement:+F2;-F2;0}");
        }
    }
    
    [ContextMenu("Clear Episode History")]
    public void ClearEpisodeHistory()
    {
        episodeHistory.Clear();
        totalEpisodes = 0;
        Debug.Log($"[{gameObject.name}] Episode history cleared.");
    }
    
    [ContextMenu("Export Episode Data")]
    public void ExportEpisodeData()
    {
        if (episodeHistory.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] No episode data to export.");
            return;
        }
        
        string csv = "Episode,Duration,Reward,EndReason,Pushes,Jumps,TimeOutside,MaxDistance,AvgSpeed,StartPos,EndPos\n";
        
        foreach (var ep in episodeHistory)
        {
            csv += $"{ep.episodeNumber},{ep.duration:F2},{ep.finalReward:F2},{ep.endReason}," +
                   $"{ep.pushesExecuted},{ep.jumpsExecuted},{ep.timeOutsideArena:F1}," +
                   $"{ep.maxDistanceFromCenter:F1},{ep.averageSpeed:F1}," +
                   $"\"{ep.startPosition}\",\"{ep.endPosition}\"\n";
        }
        
        Debug.Log($"=== EPISODE DATA CSV ===\n{csv}");
        Debug.Log($"[{gameObject.name}] Episode data exported to console. Copy the CSV section above.");
    }
    
    [ContextMenu("Test Ground Finding")]
    public void TestGroundFinding()
    {
        Vector3 testPosition = transform.position + Vector3.up * 10f;
        Vector3 groundPos = FindGroundPosition(testPosition);
        Debug.Log($"Ground test: {testPosition} -> {groundPos}");
        
        // Visualize in scene
        Debug.DrawLine(testPosition, groundPos, Color.yellow, 2f);
    }
    
    [ContextMenu("Gather Objects Now")]
    [System.Obsolete]
    public void ForceGatherObjects()
    {
        Debug.Log($"[{gameObject.name}] Forcing object gathering...");
        lastGatherTime = 0f; // Force update
        GatherGameObjects();
        UpdateObjectCache();
        DebugAgentStatus();
    }
    
    // Performance monitoring
    private void OnValidate()
    {
        // Clamp values to reasonable ranges
        decisionsPerSecond = Mathf.Clamp(decisionsPerSecond, 0.1f, 60f);
        moveSpeed = Mathf.Clamp(moveSpeed, 0.1f, 50f);
        maxSpeed = Mathf.Max(maxSpeed, moveSpeed);
        
        // Update decision interval when values change in inspector
        if (Application.isPlaying)
        {
            decisionInterval = everyFrameDecisions ? 0f : (1f / Mathf.Max(0.1f, decisionsPerSecond));
        }
    }
    
    // Cleanup
    void OnDestroy()
    {
        if (debugMovement)
            Debug.Log($"[{gameObject.name}] Agent destroyed");
    }
}