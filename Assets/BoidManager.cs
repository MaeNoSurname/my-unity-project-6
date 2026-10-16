using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    public List<BoidBehaviour> boids = new();

    [Min(3f)] public float PlayRadius = 25f;
    [Min(0f)] public float BoundaryMargin = 1f;
    public float BoundaryWeight = 7f;

    public float DestinationWeight = 0.7f;
    public float FlockTravelSpeed = 2f;
    public float MinWaypointSeconds = 5f;
    public float MaxWaypointSeconds = 12f;

    public float NeighbourRadius = 8f;
    public float SeparationRadius = 2.5f;
    public int NearestBoidCount = 3;

    public float SeparationWeight = 2.8f;
    public float AlignmentWeight = 1.2f;
    public float CohesionWeight = 0.7f;
    public float WanderWeight = 0.3f;
    public float ForwardWeight = 0.3f;

    public bool AdaptiveSwarm = true;
    [Min(1)] public int MaxAttackGroupSize = 24;
    [Min(2)] public int PopulationForMaxCoordination = 100;
    [Range(0.25f, 1f)] public float GroupGrowthExponent = 0.5f;
    [Min(1f)] public float MaxNeighbourRadiusMultiplier = 1.75f;
    [Min(1f)] public float MaxAlignmentMultiplier = 2f;
    [Min(1f)] public float MaxCohesionMultiplier = 3f;
    [Range(0f, 1f)] public float AttackGroupFlockingMultiplier = 0.4f;
    [Range(0f, 1f)] public float LargeFlockWanderMultiplier = 0.2f;

    public float SpeedMultiplier = 1f;
    public float Acceleration = 5f;
    public float RotationSmoothing = 4f;
    public float BankMultiplier = 0.6f;
    public float MaxBankAngle = 40f;
    public float SpeedVariation = 0.06f;

    [Range(0.1f, 0.8f)] public float TargetMinRadius = 0.18f;
    [Range(0.2f, 0.9f)] public float TargetMaxRadius = 0.50f;
    [Range(0.1f, 1f)] public float BoundaryStartRatio = 0.62f;
    [Min(0f)] public float BoundaryInwardBias = 0.5f;

    public Transform PlayerTarget;

    [Min(1)] public int AttackGroupSize = 3;
    [Min(0.1f)] public float GroupApproachDuration = 2f;
    [Min(0.1f)] public float GroupStrafeDuration = 4f;
    [Min(0.1f)] public float GroupDisengageDuration = 2f;
    [Min(1f)] public float GroupStagingDistance = 10f;
    [Min(0f)] public float GroupStagingSpacing = 7f;
    public float GroupStagingHeight = 5f;

    [Min(1f)] public float AttackDistance = 12f;
    public float AttackHeight = 3f;
    [Min(0f)] public float AttackStrafeWidth = 8f;
    [Min(0f)] public float FormationSpacing = 3.5f;
    [Min(0f)] public float FormationVerticalSpacing = 2.5f;

    public bool RotateStrafeAxis = false;
    [Min(0f)] public float StrafeOrbitSpeed = 0.15f;
    [Min(0f)] public float AttackSlotWeight = 3f;

    public GameObject ProjectilePrefab;
    [Min(0.05f)] public float FireInterval = 0.8f;
    [Min(1f)] public float ProjectileSpeed = 30f;
    [Range(1f, 90f)] public float FireAlignmentAngle = 15f;
    public Vector3 MuzzleOffset = new Vector3(0f, 0f, 1.5f);

    private readonly Dictionary<BoidBehaviour, float> nextShotTimes = new();
    private readonly Dictionary<BoidBehaviour, int> groupIndexCache = new();

    private struct BoidDistance
    {
        public BoidBehaviour boid;
        public float distanceSquared;

        public BoidDistance(BoidBehaviour boid, float distanceSquared)
        {
            this.boid = boid;
            this.distanceSquared = distanceSquared;
        }
    }

    private readonly List<BoidDistance> distances = new();

    private Vector3 roamingTarget;
    private Vector3 nextWaypoint;
    private float nextWaypointChange;

    private float SafeRadius => Mathf.Max(2f, PlayRadius - BoundaryMargin);
    private float FlightRadius => SafeRadius;

    private float GroupCycleDuration =>
        Mathf.Max(0.1f, GroupApproachDuration)
        + Mathf.Max(0.1f, GroupStrafeDuration)
        + Mathf.Max(0.1f, GroupDisengageDuration);

    private int EffectiveAttackGroupSize
    {
        get
        {
            int baseSize = Mathf.Max(1, AttackGroupSize);

            if (!AdaptiveSwarm)
                return baseSize;

            float populationRatio = Mathf.Max(
                1f,
                boids.Count / (float)baseSize
            );

            float growth = Mathf.Pow(
                populationRatio,
                Mathf.Clamp(GroupGrowthExponent, 0.25f, 1f)
            );

            int scaledSize = Mathf.RoundToInt(baseSize * growth);

            return Mathf.Clamp(
                scaledSize,
                baseSize,
                Mathf.Max(baseSize, MaxAttackGroupSize)
            );
        }
    }

    private float CoordinationFactor
    {
        get
        {
            if (!AdaptiveSwarm)
                return 0f;

            int startPopulation = Mathf.Max(1, AttackGroupSize);

            int maxPopulation = Mathf.Max(
                startPopulation + 1,
                PopulationForMaxCoordination
            );

            return Mathf.InverseLerp(
                startPopulation,
                maxPopulation,
                boids.Count
            );
        }
    }

    private float EffectiveNeighbourRadius =>
        Mathf.Max(0.1f, NeighbourRadius) *
        Mathf.Lerp(
            1f,
            Mathf.Max(1f, MaxNeighbourRadiusMultiplier),
            CoordinationFactor
        );

    private float EffectiveAlignmentWeight =>
        AlignmentWeight *
        Mathf.Lerp(
            1f,
            Mathf.Max(1f, MaxAlignmentMultiplier),
            CoordinationFactor
        );

    private float EffectiveCohesionWeight =>
        CohesionWeight *
        Mathf.Lerp(
            1f,
            Mathf.Max(1f, MaxCohesionMultiplier),
            CoordinationFactor
        );

    private float EffectiveWanderWeight =>
        WanderWeight *
        Mathf.Lerp(
            1f,
            Mathf.Clamp01(LargeFlockWanderMultiplier),
            CoordinationFactor
        );

    private void RebuildGroupIndexCache()
    {
        groupIndexCache.Clear();

        int groupSize = EffectiveAttackGroupSize;

        for (int i = 0; i < boids.Count; i++)
        {
            if (boids[i] != null)
                groupIndexCache[boids[i]] = i / groupSize;
        }
    }

    private void Start()
    {
        RefreshBoids();

        roamingTarget = transform.position;
        ChooseNextWaypoint();
    }

    public void RegisterBoid(BoidBehaviour boid)
    {
        if (boid == null)
            return;

        if (boid.Manager != null && boid.Manager != this)
            boid.Manager.UnregisterBoid(boid);

        boid.Manager = this;

        if (!boids.Contains(boid))
            boids.Add(boid);
    }

    public void UnregisterBoid(BoidBehaviour boid)
    {
        boids.Remove(boid);
        groupIndexCache.Remove(boid);

        if (boid != null)
        {
            nextShotTimes.Remove(boid);

            if (boid.Manager == this)
                boid.Manager = null;
        }
    }

    [ContextMenu("Refresh Boids")]
    public void RefreshBoids()
    {
        boids.RemoveAll(b => b == null || b.Manager != this);

        BoidBehaviour[] found = FindObjectsByType<BoidBehaviour>(
            FindObjectsSortMode.None
        );

        foreach (BoidBehaviour boid in found)
        {
            if (boid.Manager == this && !boids.Contains(boid))
                boids.Add(boid);
        }

        RebuildGroupIndexCache();
    }

    private void FixedUpdate()
    {
        boids.RemoveAll(b => b == null || b.Manager != this);

        RebuildGroupIndexCache();
        UpdateRoamingTarget();
        KeepBoidsInsidePlayArea();

        foreach (BoidBehaviour boid in boids)
        {
            if (boid != null && boid.rb != null)
                FindNeighbours(boid);
        }

        foreach (BoidBehaviour boid in boids)
        {
            if (boid != null && boid.rb != null)
                CalculateSteering(boid);
        }

        foreach (BoidBehaviour boid in boids)
        {
            if (boid != null && boid.rb != null)
                Fly(boid);
        }

        UpdateShooting();
    }

    private void ChooseNextWaypoint()
    {
        Vector3 direction = Random.insideUnitSphere;

        if (direction.sqrMagnitude < 0.001f)
            direction = Random.onUnitSphere;

        direction.Normalize();

        float minRadius = Mathf.Min(TargetMinRadius, TargetMaxRadius);
        float maxRadius = Mathf.Max(TargetMinRadius, TargetMaxRadius);

        float targetRadius = SafeRadius * Random.Range(minRadius, maxRadius);

        nextWaypoint = transform.position + direction * targetRadius;

        nextWaypointChange = Time.time + Random.Range(
            Mathf.Min(MinWaypointSeconds, MaxWaypointSeconds),
            Mathf.Max(MinWaypointSeconds, MaxWaypointSeconds)
        );
    }

    private void UpdateRoamingTarget()
    {
        float distance = Vector3.Distance(roamingTarget, nextWaypoint);

        if (distance < 2f || Time.time >= nextWaypointChange)
            ChooseNextWaypoint();

        roamingTarget = Vector3.MoveTowards(
            roamingTarget,
            nextWaypoint,
            FlockTravelSpeed * Time.fixedDeltaTime
        );

        roamingTarget = ClampToPlayArea(roamingTarget);
    }

    private void FindNeighbours(BoidBehaviour boid)
    {
        boid.NearestBoids.Clear();
        boid.BoidsWithinRadius.Clear();
        distances.Clear();

        Vector3 position = boid.rb.position;
        float neighbourRadius = EffectiveNeighbourRadius;
        float radiusSquared = neighbourRadius * neighbourRadius;

        foreach (BoidBehaviour other in boids)
        {
            if (other == null || other == boid || other.rb == null)
                continue;

            float distanceSquared = (other.rb.position - position).sqrMagnitude;

            distances.Add(new BoidDistance(other, distanceSquared));

            if (distanceSquared <= radiusSquared)
                boid.BoidsWithinRadius.Add(other);
        }

        distances.Sort((a, b) => a.distanceSquared.CompareTo(b.distanceSquared));

        int baseNearestCount = Mathf.Max(0, NearestBoidCount);
        int maxNearestCount = Mathf.Max(baseNearestCount, EffectiveAttackGroupSize);

        int requestedCount = Mathf.RoundToInt(
            Mathf.Lerp(baseNearestCount, maxNearestCount, CoordinationFactor)
        );

        int count = Mathf.Min(requestedCount, distances.Count);

        for (int i = 0; i < count; i++)
            boid.NearestBoids.Add(distances[i].boid);
    }

    private void GetAttackSchedule(
        out int activeGroup,
        out float phaseTime,
        out int groupCount)
    {
        int groupSize = EffectiveAttackGroupSize;

        groupCount = Mathf.Max(
            1,
            Mathf.CeilToInt(boids.Count / (float)groupSize)
        );

        float cycleDuration = GroupCycleDuration;

        int turnIndex = Mathf.FloorToInt(Time.time / cycleDuration);
        activeGroup = turnIndex % groupCount;
        phaseTime = Mathf.Repeat(Time.time, cycleDuration);
    }

    private int GetGroupIndex(BoidBehaviour boid)
    {
        if (boid == null)
            return 0;

        if (groupIndexCache.TryGetValue(boid, out int cachedGroup))
            return cachedGroup;

        int index = boids.IndexOf(boid);

        if (index < 0)
            return 0;

        return index / EffectiveAttackGroupSize;
    }

    private void GetGroupSlotOffset(
        BoidBehaviour boid,
        int groupIndex,
        out float lateralSlot,
        out float verticalSlot)
    {
        int index = boids.IndexOf(boid);

        if (index < 0)
            index = 0;

        int groupSize = EffectiveAttackGroupSize;
        int firstIndex = groupIndex * groupSize;

        int membersInGroup = Mathf.Min(
            groupSize,
            Mathf.Max(1, boids.Count - firstIndex)
        );

        int localIndex = index - firstIndex;

        int columns = Mathf.Max(
            1,
            Mathf.CeilToInt(Mathf.Sqrt(membersInGroup))
        );

        int rows = Mathf.CeilToInt(membersInGroup / (float)columns);
        int row = localIndex / columns;
        int col = localIndex % columns;

        int columnsInRow = Mathf.Min(
            columns,
            membersInGroup - row * columns
        );

        lateralSlot = (col - (columnsInRow - 1) * 0.5f) * FormationSpacing;
        verticalSlot = ((rows - 1) * 0.5f - row) * FormationVerticalSpacing;
    }

    private void GetAttackAxes(out Vector3 away, out Vector3 right)
    {
        away = transform.position - PlayerTarget.position;

        if (away.sqrMagnitude < 0.001f)
            away = PlayerTarget.forward;

        if (away.sqrMagnitude < 0.001f)
            away = Vector3.forward;

        away.Normalize();

        right = Vector3.Cross(Vector3.up, away);

        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(PlayerTarget.up, away);

        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;

        right.Normalize();

        if (RotateStrafeAxis)
        {
            Quaternion rotation = Quaternion.AngleAxis(
                Time.time * StrafeOrbitSpeed * 360f,
                Vector3.up
            );

            away = rotation * away;
            right = rotation * right;
        }
    }

    private Vector3 GetStagingTarget(
        int groupIndex,
        int groupCount,
        float lateralSlot,
        float verticalSlot,
        Vector3 away,
        Vector3 right)
    {
        float centredGroup = groupIndex - (groupCount - 1) * 0.5f;
        float groupLateral = centredGroup * GroupStagingSpacing;

        return PlayerTarget.position
            + away * (AttackDistance + GroupStagingDistance)
            + right * (groupLateral + lateralSlot)
            + Vector3.up * (GroupStagingHeight + verticalSlot);
    }

    private Vector3 GetGroupTarget(
        BoidBehaviour boid,
        int groupIndex,
        int activeGroup,
        int groupCount,
        float phaseTime)
    {
        GetAttackAxes(out Vector3 away, out Vector3 right);

        GetGroupSlotOffset(
            boid,
            groupIndex,
            out float lateralSlot,
            out float verticalSlot
        );

        Vector3 stagingTarget = GetStagingTarget(
            groupIndex,
            groupCount,
            lateralSlot,
            verticalSlot,
            away,
            right
        );

        if (groupIndex != activeGroup)
            return stagingTarget;

        float approachDuration = Mathf.Max(0.1f, GroupApproachDuration);
        float strafeDuration = Mathf.Max(0.1f, GroupStrafeDuration);

        float disengageStart = approachDuration + strafeDuration;
        float attackHeight = AttackHeight + verticalSlot;

        if (phaseTime < approachDuration)
        {
            float t = Mathf.Clamp01(phaseTime / approachDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            float distance = Mathf.Lerp(
                AttackDistance + GroupStagingDistance,
                AttackDistance,
                t
            );

            return PlayerTarget.position
                + away * distance
                + right * (-AttackStrafeWidth + lateralSlot)
                + Vector3.up * attackHeight;
        }

        if (phaseTime < disengageStart)
        {
            float t = Mathf.Clamp01(
                (phaseTime - approachDuration) / strafeDuration
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            float lateral = Mathf.Lerp(
                -AttackStrafeWidth,
                AttackStrafeWidth,
                t
            ) + lateralSlot;

            return PlayerTarget.position
                + away * AttackDistance
                + right * lateral
                + Vector3.up * attackHeight;
        }

        float disengageDuration = Mathf.Max(0.1f, GroupDisengageDuration);

        float disengageT = Mathf.Clamp01(
            (phaseTime - disengageStart) / disengageDuration
        );

        disengageT = Mathf.SmoothStep(0f, 1f, disengageT);

        Vector3 strafeEndTarget = PlayerTarget.position
            + away * AttackDistance
            + right * (AttackStrafeWidth + lateralSlot)
            + Vector3.up * attackHeight;

        return Vector3.Lerp(strafeEndTarget, stagingTarget, disengageT);
    }

    private bool IsGroupAllowedToShoot(BoidBehaviour boid)
    {
        if (PlayerTarget == null)
            return false;

        GetAttackSchedule(out int activeGroup, out float phaseTime, out _);

        int groupIndex = GetGroupIndex(boid);

        if (groupIndex != activeGroup)
            return false;

        float strafeStart = Mathf.Max(0.1f, GroupApproachDuration);
        float strafeEnd = strafeStart + Mathf.Max(0.1f, GroupStrafeDuration);

        return phaseTime >= strafeStart && phaseTime < strafeEnd;
    }

    private void CalculateSteering(BoidBehaviour boid)
    {
        Vector3 position = boid.rb.position;
        Vector3 forward = boid.FacingDirection.normalized;

        if (forward.sqrMagnitude < 0.001f)
            forward = boid.transform.forward;

        bool attacking = PlayerTarget != null;
        Vector3 steering = forward * ForwardWeight;
        int myGroupIndex = -1;

        if (attacking)
        {
            GetAttackSchedule(
                out int activeGroup,
                out float phaseTime,
                out int groupCount
            );

            myGroupIndex = GetGroupIndex(boid);

            Vector3 target = GetGroupTarget(
                boid,
                myGroupIndex,
                activeGroup,
                groupCount,
                phaseTime
            );

            Vector3 toTarget = target - position;

            if (toTarget.sqrMagnitude > 0.01f)
                steering = toTarget.normalized * AttackSlotWeight;
        }
        else
        {
            Vector3 toDestination = roamingTarget - position;
            float destinationDistance = toDestination.magnitude;

            if (destinationDistance > 0.1f)
            {
                float influence = Mathf.Clamp01(destinationDistance / 8f);

                steering += toDestination.normalized * DestinationWeight * influence;
            }
        }

        Vector3 alignment = Vector3.zero;
        Vector3 flockCentre = Vector3.zero;
        Vector3 separation = Vector3.zero;

        int flockNeighbourCount = 0;

        float separationRadius = Mathf.Max(SeparationRadius, 0.1f);
        float separationSquared = separationRadius * separationRadius;

        foreach (BoidBehaviour other in boid.BoidsWithinRadius)
        {
            if (other == null || other.rb == null)
                continue;

            Vector3 offset = other.rb.position - position;
            float distanceSquared = offset.sqrMagnitude;

            if (distanceSquared < 0.0001f)
                continue;

            float distance = Mathf.Sqrt(distanceSquared);

            if (distanceSquared < separationSquared)
            {
                float closeness = 1f - distance / separationRadius;

                separation -= offset.normalized *
                    (closeness / Mathf.Max(distance, 0.5f));
            }

            bool sameGroup = !attacking || GetGroupIndex(other) == myGroupIndex;

            if (sameGroup)
            {
                alignment += other.FacingDirection.normalized;
                flockCentre += other.rb.position;
                flockNeighbourCount++;
            }
        }

        separation = Vector3.ClampMagnitude(separation, 2f);
        steering += separation * SeparationWeight;

        if (flockNeighbourCount > 0)
        {
            alignment = (alignment / flockNeighbourCount).normalized;

            Vector3 averageNeighbourPosition = flockCentre / flockNeighbourCount;
            Vector3 cohesion = (averageNeighbourPosition - position).normalized;

            float groupFlockingScale = attacking
                ? Mathf.Clamp01(AttackGroupFlockingMultiplier)
                : 1f;

            steering += alignment * EffectiveAlignmentWeight * groupFlockingScale;
            steering += cohesion * EffectiveCohesionWeight * groupFlockingScale;
        }

        if (!attacking)
        {
            if (Time.time >= boid.NextWanderChange)
            {
                boid.WanderDirection = Random.onUnitSphere;
                boid.NextWanderChange = Time.time + Random.Range(1.5f, 3.5f);
            }

            steering += boid.WanderDirection.normalized * EffectiveWanderWeight;
        }

        Vector3 relativePosition = position - transform.position;
        float distanceFromCentre = relativePosition.magnitude;

        float boundaryStart = FlightRadius * Mathf.Clamp01(BoundaryStartRatio);
        float boundaryEnd = FlightRadius - 0.25f;

        if (distanceFromCentre > boundaryStart && distanceFromCentre > 0.001f)
        {
            float urgency = Mathf.InverseLerp(
                boundaryStart,
                Mathf.Max(boundaryStart + 0.01f, boundaryEnd),
                distanceFromCentre
            );

            steering -= relativePosition.normalized * BoundaryWeight * urgency * urgency;
        }

        boid.DesiredDirection = steering.sqrMagnitude > 0.001f
            ? steering.normalized
            : forward;
    }

    private void Fly(BoidBehaviour boid)
    {
        float dt = Time.fixedDeltaTime;
        bool attacking = PlayerTarget != null;

        Vector3 forward = boid.FacingDirection.normalized;

        if (forward.sqrMagnitude < 0.001f)
            forward = boid.transform.forward;

        Vector3 desired = boid.DesiredDirection.normalized;

        if (desired.sqrMagnitude < 0.001f)
            desired = forward;

        Vector3 movementForward = forward;

        if (attacking && boid.rb.linearVelocity.sqrMagnitude > 0.01f)
            movementForward = boid.rb.linearVelocity.normalized;

        Vector3 movementDirection = Vector3.RotateTowards(
            movementForward,
            desired,
            boid.TurnSpeed * Mathf.Deg2Rad * dt,
            0f
        ).normalized;

        float speedVariation = 1f
            + Mathf.Sin(Time.time * 0.7f + boid.SpeedPhase) * SpeedVariation;

        float targetSpeed = boid.Speed * SpeedMultiplier * speedVariation;
        Vector3 targetVelocity = movementDirection * targetSpeed;

        float maxSpeed = Mathf.Max(
            0f,
            boid.Speed * SpeedMultiplier * (1f + Mathf.Abs(SpeedVariation))
        );

        Vector3 currentVelocity = Vector3.ClampMagnitude(
            boid.rb.linearVelocity,
            maxSpeed
        );

        Vector3 velocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            Acceleration * dt
        );

        Vector3 relativePosition = boid.rb.position - transform.position;
        Vector3 predictedOffset = relativePosition + velocity * dt;

        if (predictedOffset.sqrMagnitude > FlightRadius * FlightRadius
            && relativePosition.sqrMagnitude > 0.001f)
        {
            Vector3 boundaryNormal = relativePosition.normalized;
            float outwardSpeed = Vector3.Dot(velocity, boundaryNormal);

            if (outwardSpeed > 0f)
                velocity -= boundaryNormal * (outwardSpeed + BoundaryInwardBias);
        }

        if (velocity.sqrMagnitude > 0.01f)
            movementDirection = velocity.normalized;

        Vector3 facingDirection;
        float bankAngle = 0f;

        if (attacking)
        {
            facingDirection = PlayerTarget.position - boid.rb.position;

            if (facingDirection.sqrMagnitude < 0.001f)
                facingDirection = forward;

            facingDirection.Normalize();

            GetAttackAxes(out _, out Vector3 attackRight);

            float sidewaysAmount = Vector3.Dot(movementDirection, attackRight);

            bankAngle = Mathf.Clamp(
                -sidewaysAmount * MaxBankAngle * BankMultiplier,
                -MaxBankAngle,
                MaxBankAngle
            );
        }
        else
        {
            facingDirection = movementDirection;

            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            Vector3 flatDesired = Vector3.ProjectOnPlane(movementDirection, Vector3.up);

            float turnAngle = 0f;

            if (flatForward.sqrMagnitude > 0.001f &&
                flatDesired.sqrMagnitude > 0.001f)
            {
                turnAngle = Vector3.SignedAngle(
                    flatForward,
                    flatDesired,
                    Vector3.up
                );
            }

            bankAngle = Mathf.Clamp(
                -turnAngle * BankMultiplier,
                -MaxBankAngle,
                MaxBankAngle
            );
        }

        Vector3 upReference = Mathf.Abs(
            Vector3.Dot(facingDirection, Vector3.up)
        ) > 0.95f ? Vector3.forward : Vector3.up;

        Quaternion facingRotation = Quaternion.LookRotation(
            facingDirection,
            upReference
        );

        Quaternion targetRotation = facingRotation *
            Quaternion.AngleAxis(bankAngle, Vector3.forward);

        float blend = 1f - Mathf.Exp(-RotationSmoothing * dt);

        boid.rb.MoveRotation(
            Quaternion.Slerp(boid.rb.rotation, targetRotation, blend)
        );

        boid.rb.linearVelocity = velocity;
        boid.FacingDirection = facingDirection;
    }

    private void UpdateShooting()
    {
        if (PlayerTarget == null || ProjectilePrefab == null)
            return;

        foreach (BoidBehaviour boid in boids)
        {
            if (boid == null || boid.rb == null)
                continue;

            if (!IsGroupAllowedToShoot(boid))
                continue;

            if (nextShotTimes.TryGetValue(boid, out float nextShotTime) &&
                Time.time < nextShotTime)
            {
                continue;
            }

            if (FireForward(boid))
                nextShotTimes[boid] = Time.time + Mathf.Max(0.05f, FireInterval);
        }
    }

    private bool FireForward(BoidBehaviour boid)
    {
        if (PlayerTarget == null || ProjectilePrefab == null)
            return false;

        Quaternion shipRotation = boid.rb.rotation;
        Vector3 shipForward = (shipRotation * Vector3.forward).normalized;
        Vector3 muzzlePosition = boid.rb.position + shipRotation * MuzzleOffset;

        Vector3 toPlayer = PlayerTarget.position - muzzlePosition;

        if (toPlayer.sqrMagnitude < 0.001f)
            return false;

        toPlayer.Normalize();

        float alignment = Vector3.Dot(shipForward, toPlayer);

        float minimumAlignment = Mathf.Cos(
            Mathf.Clamp(FireAlignmentAngle, 1f, 90f) * Mathf.Deg2Rad
        );

        if (alignment < minimumAlignment)
            return false;

        GameObject projectile = Instantiate(
            ProjectilePrefab,
            muzzlePosition,
            shipRotation
        );

        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();

        if (projectileRb == null)
        {
            Debug.LogError(
                "ProjectilePrefab needs a Rigidbody on its root object.",
                projectile
            );

            Destroy(projectile);
            return false;
        }

        projectileRb.useGravity = false;
        projectileRb.linearVelocity = shipForward * Mathf.Max(ProjectileSpeed, 1f);

        Collider[] shipColliders = boid.GetComponentsInChildren<Collider>();
        Collider[] projectileColliders = projectile.GetComponentsInChildren<Collider>();

        foreach (Collider shipCollider in shipColliders)
        {
            if (shipCollider == null)
                continue;

            foreach (Collider projectileCollider in projectileColliders)
            {
                if (projectileCollider == null)
                    continue;

                Physics.IgnoreCollision(shipCollider, projectileCollider);
            }
        }

        return true;
    }

    private Vector3 ClampToPlayArea(Vector3 worldPosition)
    {
        Vector3 offset = worldPosition - transform.position;

        if (offset.sqrMagnitude > FlightRadius * FlightRadius)
            offset = offset.normalized * FlightRadius;

        return transform.position + offset;
    }

    private void KeepBoidsInsidePlayArea()
    {
        foreach (BoidBehaviour boid in boids)
        {
            if (boid == null || boid.rb == null)
                continue;

            Vector3 safePosition = ClampToPlayArea(boid.rb.position);

            if ((safePosition - boid.rb.position).sqrMagnitude > 0.000001f)
                boid.rb.position = safePosition;

            Vector3 relative = boid.rb.position - transform.position;
            Vector3 velocity = boid.rb.linearVelocity;

            if (relative.magnitude >= FlightRadius - 0.01f &&
                relative.sqrMagnitude > 0.001f)
            {
                Vector3 normal = relative.normalized;
                float outwardSpeed = Vector3.Dot(velocity, normal);

                if (outwardSpeed > 0f)
                    velocity -= normal * (outwardSpeed + BoundaryInwardBias);
                else
                    velocity -= normal * (BoundaryInwardBias * 0.25f);
            }

            boid.rb.linearVelocity = velocity;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 centre = transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(centre, Mathf.Max(3f, PlayRadius));

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(centre, FlightRadius);

        if (!Application.isPlaying)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(roamingTarget, 0.35f);
        Gizmos.DrawLine(centre, roamingTarget);

        if (PlayerTarget == null)
            return;

        GetAttackAxes(out Vector3 away, out Vector3 right);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(PlayerTarget.position, 0.6f);

        Vector3 laneCentre = PlayerTarget.position + away * AttackDistance;
        Vector3 laneStart = laneCentre - right * AttackStrafeWidth;
        Vector3 laneEnd = laneCentre + right * AttackStrafeWidth;

        Gizmos.DrawLine(laneStart, laneEnd);
        Gizmos.DrawWireSphere(laneStart, 0.3f);
        Gizmos.DrawWireSphere(laneEnd, 0.3f);
    }
}