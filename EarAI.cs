using GameNetcodeStuff;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TheEar
{
    public class EarAI : EnemyAI , INoiseListener
    {
        public AudioSource footstepSFX;
        public AudioSource breathingSFX;
        public AudioSource tentacleSFX;
        public AudioSource distantSFX;

        public AudioClip[] distantScreaming;
        public AudioClip[] alertedScreaming;
        public AudioClip[] wanderingNoises;
        public AudioClip calmerBreathing;
        public AudioClip frenziedBreathing;
        public AudioClip mouthScream;
        public AudioClip [] goreClips;
        public AudioClip[] tentacleClips;

        public GameObject tentacleL1Drop;
        public GameObject tentacleL2Drop;
        public GameObject tentacleRDrop;
        public GameObject tentacleMouthStink;
        public GameObject tentacleMouthStink2;
        public GameObject tentacleMouthStink3;
        public GameObject earCanalGunk;

        private int earHealth = 3;
        private float wanderSpeed = 5f;
        private float investigateSpeed = 6f;
        private float dashSpeed = 8.5f;

        public AISearchRoutine wanderFacility;
        private System.Random enemyRandom;
        public Transform headPos;
        public Transform torsoPos;
        private float hearNoiseCooldown;
        public int suspicionLevel;
        private float noiseApproximation = 14f;
        private Vector3 previousPosition;
        private Vector3 lastHeardNoisePosition;
        private Vector3 noisePositionGuess;
        private bool hasStartedKillAnim = false;
        private float killAnimTime;
        private bool hasPlayedGunk = false;
        private PlayerControllerB targetPlayerHolder;
        private Coroutine killPlayerCoroutine;
        private bool isEnraged = false;
        private float suspicionTimer;
        private RoundManager roundManager;
        private float lastHeardNoiseDistanceWhenHeard;
        private float wanderNoiseTimer;


        //Behaviour States
        //0 - Calm
        //1 - Suspicious
        //2 - Enraged
        //3 - Executing
        // ADD STATE THAT ALLOWS THEM TO BE STUNNED BY LOUD NOISES

        public override void Start()
        {
            base.Start();
            roundManager = UnityEngine.Object.FindObjectOfType<RoundManager>();
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            enemyHP = earHealth;
            if (breathingSFX != null && calmerBreathing != null)
            {
                breathingSFX.clip = calmerBreathing;
                breathingSFX.Play();
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
        }

        public override void Update()
        {
            base.Update();
            if (isEnemyDead) return;
            if (currentBehaviourStateIndex == 2 || currentBehaviourStateIndex == 3)
            {
                if (tentacleSFX != null && tentacleClips != null && tentacleClips.Length > 0 && !tentacleSFX.isPlaying)
                {
                    tentacleSFX.clip = tentacleClips[enemyRandom.Next(0, tentacleClips.Length)];
                    tentacleSFX.Play();
                }
                if (breathingSFX != null && frenziedBreathing != null && breathingSFX.clip != frenziedBreathing)
                {
                    breathingSFX.clip = frenziedBreathing;
                    breathingSFX.Play();
                }
            }
            else
            {
                if (tentacleSFX != null && tentacleSFX.isPlaying)
                {
                    tentacleSFX.Stop();
                }
                if (breathingSFX != null && calmerBreathing != null && breathingSFX.clip != calmerBreathing)
                {
                    breathingSFX.clip = calmerBreathing;
                    breathingSFX.Play();
                }
            }
            if (currentBehaviourStateIndex == 1)
            {
                if (!hasPlayedGunk)
                {
                    hasPlayedGunk = true;
                    if (earCanalGunk != null)
                    {
                        ParticleSystem[] particles = earCanalGunk.GetComponentsInChildren<ParticleSystem>();
                        for (int i = 0; i < particles.Length; i++)
                        {
                            particles[i].Play();
                        }
                    }
                }
            }
            else if (currentBehaviourStateIndex == 0)
            {
                hasPlayedGunk = false;
            }
            bool shouldEnableDrops = (currentBehaviourStateIndex == 2 || currentBehaviourStateIndex == 3);
            if (tentacleL1Drop != null && tentacleL1Drop.activeSelf != shouldEnableDrops) tentacleL1Drop.SetActive(shouldEnableDrops);
            if (tentacleL2Drop != null && tentacleL2Drop.activeSelf != shouldEnableDrops) tentacleL2Drop.SetActive(shouldEnableDrops);
            if (tentacleRDrop != null && tentacleRDrop.activeSelf != shouldEnableDrops) tentacleRDrop.SetActive(shouldEnableDrops);
            bool shouldEnableStink = (currentBehaviourStateIndex == 3);
            if (tentacleMouthStink != null && tentacleMouthStink.activeSelf != shouldEnableStink) tentacleMouthStink.SetActive(shouldEnableStink);
            if (tentacleMouthStink2 != null && tentacleMouthStink2.activeSelf != shouldEnableStink) tentacleMouthStink2.SetActive(shouldEnableStink);
            if (tentacleMouthStink3 != null && tentacleMouthStink3.activeSelf != shouldEnableStink) tentacleMouthStink3.SetActive(shouldEnableStink);


            hearNoiseCooldown -= Time.deltaTime;
            previousPosition = base.transform.position;

            if (hasStartedKillAnim)
            {
                killAnimTime -= Time.deltaTime;
            }
            else
            {
                killAnimTime = 0f;
            }

            if (stunNormalizedTimer > 0f)
            {
                agent.speed = 0f;
                if (creatureAnimator != null)
                {
                    creatureAnimator.SetLayerWeight(1, 1f);
                }
                if (hasStartedKillAnim && IsServer)
                {
                    StopKillAnimationServerRpc(-1);
                }
            }
            else
            {
                if (creatureAnimator != null)
                {
                    creatureAnimator.SetLayerWeight(1, 0f);
                }
            }

            switch (currentBehaviourStateIndex)
            {
                case 0:
                    if (base.IsOwner)
                    {
                        agent.speed = wanderSpeed;
                        if (stunNormalizedTimer > 0f)
                        {
                            agent.speed = 0f;
                        }
                        if (base.IsOwner && !wanderFacility.inProgress)
                        {
                            StartSearch(base.transform.position, wanderFacility);
                        }
                    }
                    wanderNoiseTimer -= Time.deltaTime;
                    if (wanderNoiseTimer <= 0f)
                    {
                        wanderNoiseTimer = UnityEngine.Random.Range(8f, 15f);
                        if (wanderingNoises != null && wanderingNoises.Length > 0 && creatureSFX != null)
                        {
                            creatureSFX.PlayOneShot(wanderingNoises[enemyRandom.Next(0, wanderingNoises.Length)]);
                        }
                    }
                    break;
                case 1:
                    if (isEnraged)
                    {
                        isEnraged = false;
                    }
                    if (!base.IsOwner)
                    {
                        break;
                    }
                    if (base.IsOwner && wanderFacility.inProgress)
                    {
                        StopSearch(wanderFacility);
                    }
                    agent.speed = investigateSpeed;
                    if (stunNormalizedTimer > 0f)
                    {
                        agent.speed = 0f;
                    }
                    if (Vector3.Distance(base.transform.position, agent.destination) < 3f)
                    {
                        SearchForPreviouslyHeardSound();
                    }
                    suspicionTimer -= Time.deltaTime;
                    if (suspicionTimer <= 0f)
                    {
                        suspicionTimer = 4f;
                        suspicionLevel--;
                        if (suspicionLevel <= 1)
                        {
                            SwitchToBehaviourState(0);
                        }
                    }
                    break;
                case 2:
                    if (!base.IsOwner)
                    {
                        break;
                    }
                    if (base.IsOwner && wanderFacility.inProgress)
                    {
                        StopSearch(wanderFacility);
                    }
                    if (stunNormalizedTimer > 0f)
                    {
                        agent.speed = 0f;
                    }
                    agent.speed = dashSpeed;
                    if (Vector3.Distance(base.transform.position, agent.destination) < 3f)
                    {
                        SearchForPreviouslyHeardSound();
                    }
                    suspicionTimer -= Time.deltaTime;
                    if (suspicionTimer <= 0f)
                    {
                        suspicionTimer = 3f;
                        suspicionLevel--;
                        if (suspicionLevel <= 8)
                        {
                            SwitchToBehaviourState(1);
                        }
                    }
                    break;
                case 3:
                    if(!inSpecialAnimation)
                    {
                        inSpecialAnimation = true;
                    }

                    break;

            }

            }

        //helpers

        private void SearchForPreviouslyHeardSound()
        {
            int num = 0;
            Vector3 vector = base.transform.position;
            while (num < 5 && Vector3.Distance(vector, base.transform.position) < 4f)
            {
                num++;
                vector = roundManager.GetRandomNavMeshPositionInRadius(lastHeardNoisePosition, 10f);
            }
            SetDestinationToPosition(vector);
            noisePositionGuess = vector;
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesNoisePlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
            if (isEnemyDead || stunNormalizedTimer > 0f || hearNoiseCooldown > 0f || hasStartedKillAnim)
            {
                return;
            }
            hearNoiseCooldown = 0.03f;
            float distance = Vector3.Distance(transform.position, noisePosition);
            float hearRange = 35f * noiseLoudness;
            if (Physics.Linecast(transform.position, noisePosition, 256))
            {
                noiseLoudness /= 2f;
                hearRange /= 2f;
            }
            if (noiseLoudness < 0.25f)
            {
                return;
            }
            if (distance < hearRange)
            {
                if (currentBehaviourStateIndex < 2)
                {
                    suspicionLevel = 9;
                }
                else
                {
                    suspicionLevel++;
                }
                Vector3 guessedPos = noisePosition;
                if (distance > 2f)
                {
                    guessedPos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(noisePosition, distance / noiseApproximation);
                }
                noisePositionGuess = guessedPos;
                lastHeardNoisePosition = noisePosition;
                lastHeardNoiseDistanceWhenHeard = distance;
                AlertEar(noisePositionGuess, distance);
            }
        }

        private void AlertEar(Vector3 targetPosition, float distanceToNoise)
        {
            if (!base.IsOwner)
            {
                ChangeOwnershipOfEnemy(NetworkManager.Singleton.LocalClientId);
            }
            if (distanceToNoise < 8f)
            {
                if (currentBehaviourStateIndex < 2)
                {
                    if (currentBehaviourStateIndex == 0)
                    {
                        if (alertedScreaming != null && alertedScreaming.Length > 0 && creatureSFX != null)
                        {
                            creatureSFX.PlayOneShot(alertedScreaming[enemyRandom.Next(0, alertedScreaming.Length)]);
                        }
                        if (distantScreaming != null && distantScreaming.Length > 0 && distantSFX != null)
                        {
                            distantSFX.PlayOneShot(distantScreaming[enemyRandom.Next(0, distantScreaming.Length)]);
                        }
                    }
                    SwitchToBehaviourState(2);
                    suspicionLevel = 10;
                    if (wanderFacility != null && wanderFacility.inProgress)
                    {
                        StopSearch(wanderFacility);
                    }
                }
            }
            else if (suspicionLevel >= 5)
            {
                if (currentBehaviourStateIndex == 0)
                {
                    if (alertedScreaming != null && alertedScreaming.Length > 0 && creatureSFX != null)
                    {
                        creatureSFX.PlayOneShot(alertedScreaming[enemyRandom.Next(0, alertedScreaming.Length)]);
                    }
                    if (distantScreaming != null && distantScreaming.Length > 0 && distantSFX != null)
                    {
                        distantSFX.PlayOneShot(distantScreaming[enemyRandom.Next(0, distantScreaming.Length)]);
                    }
                    SwitchToBehaviourState(1);
                    if (wanderFacility != null && wanderFacility.inProgress)
                    {
                        StopSearch(wanderFacility);
                    }
                }
            }
            SetDestinationToPosition(targetPosition);
        }



        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            enemyHP -= force;
            if (base.IsOwner)
            {
                if (enemyHP <= 0)
                {
                    if (footstepSFX !=null && creatureVoice != null)
                    {
                        footstepSFX.Stop();
                        creatureVoice.Stop();
                    }
                    KillEnemyOnOwnerClient(); //This is called in EnemyAI, creatureAnimator.SetBool("Dead", value: true); use it in animator
                    return;
                }
                if (inSpecialAnimation)
                {
                    StopKillAnimationServerRpc(playerWhoHit != null ? (int)playerWhoHit.playerClientId : -1);
                }
            }
            if (playerWhoHit != null && IsServer)
            {
                targetPlayer = playerWhoHit;
            }
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other, hasStartedKillAnim);
            Debug.Log("EAR: Collision detected.");
            if (playerControllerB != null && !hasStartedKillAnim && !inSpecialAnimation)
            {
                if (currentBehaviourStateIndex == 2)
                {
                    playerControllerB.inAnimationWithEnemy = this;
                    GrabPlayerServerRpc((int)playerControllerB.playerClientId);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void GrabPlayerServerRpc(int playerId)
        {
            if (!hasStartedKillAnim)
            {
                hasStartedKillAnim = true;
                GrabPlayerClientRpc(playerId);
            }
            else
            {
                CancelKillAnimationWithPlayerClientRpc(playerId);
            }
        }

        [ClientRpc]
        public void GrabPlayerClientRpc(int playerId)
        {
            if (killPlayerCoroutine != null)
            {
                StopCoroutine(killPlayerCoroutine);
            }
            Debug.Log("Grabbed player!");
            targetPlayerHolder = StartOfRound.Instance.allPlayerScripts[playerId];
            inSpecialAnimation = true;
            hasStartedKillAnim = true;
            SwitchToBehaviourState(3);
            killPlayerCoroutine = StartCoroutine(executionRoutine(targetPlayerHolder));
        }

        private IEnumerator executionRoutine(PlayerControllerB targetPlayer)
        {
            if (base.IsOwner)
            {
                agent.speed = 0f;
            }
            targetPlayer.DamagePlayer(80, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling, 1);
            float duration = 5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (targetPlayer == null)
                {
                    if (IsServer)
                    {
                        StopKillAnimationServerRpc(-1);
                    }
                    yield break;
                }
                if (targetPlayer.inAnimationWithEnemy != this)
                {
                    if (IsServer)
                    {
                        StopKillAnimationServerRpc(-1);
                    }
                    yield break;
                }
                if (targetPlayer.isPlayerDead)
                {
                    if (IsServer)
                    {
                        StopKillAnimationServerRpc(-1);
                    }
                    yield break;
                }
                if (!hasStartedKillAnim || isEnemyDead)
                {
                    yield break;
                }
                float pullProgress = Mathf.Clamp01(elapsed / 3f);
                float currentDistance = Mathf.Lerp(2.0f, 0.0f, pullProgress);
                Vector3 targetPos = torsoPos.position + transform.forward * currentDistance;
                targetPos.y = Mathf.Max(targetPos.y, transform.position.y);
                targetPlayer.transform.position = targetPos;
                elapsed += Time.deltaTime;
                yield return null;
            }
            targetPlayer.DamagePlayer(100, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling, 1);
            if (IsServer)
            {
                KillFinishedServerRpc((int)targetPlayer.playerClientId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void KillFinishedServerRpc(int playerId)
        {
            if (!hasStartedKillAnim)
            {
                return;
            }
            KillFinishedClientRpc(playerId);
        }

        [ClientRpc]
        public void KillFinishedClientRpc(int playerId)
        {
            hasStartedKillAnim = false;
            inSpecialAnimation = false;
            hearNoiseCooldown = 2f;
            Debug.Log("EAR: Kill finished.");
            if (targetPlayerHolder != null)
            {
                targetPlayerHolder.inAnimationWithEnemy = null;
                targetPlayerHolder = null;
            }
            if (creatureAnimator != null)
            {
                creatureAnimator.SetBool("isExecuting", false);
            }
            SwitchToBehaviourState(2);
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopKillAnimationServerRpc(int hitterId)
        {
            StopKillAnimationClientRpc(hitterId);
        }

        [ClientRpc]
        public void StopKillAnimationClientRpc(int hitterId)
        {
            if (killPlayerCoroutine != null)
            {
                StopCoroutine(killPlayerCoroutine);
            }
            if (targetPlayerHolder != null)
            {
                targetPlayerHolder.inAnimationWithEnemy = null;
                targetPlayerHolder = null;
            }
            inSpecialAnimation = false;
            hasStartedKillAnim = false;
            if (creatureAnimator != null)
            {
                creatureAnimator.SetBool("isExecuting", false);
            }
            if (hitterId != -1)
            {
                PlayerControllerB hitter = StartOfRound.Instance.allPlayerScripts[hitterId];
                if (hitter != null && IsServer)
                {
                    targetPlayer = hitter;
                }
            }
            SwitchToBehaviourState(2);
        }

        [ClientRpc]
        public void CancelKillAnimationWithPlayerClientRpc(int playerObjectId)
        {
            StartOfRound.Instance.allPlayerScripts[playerObjectId].inAnimationWithEnemy = null;
        }

        public override void KillEnemy(bool destroy = false)
        {
            base.KillEnemy(destroy);
            if (distantSFX != null) distantSFX.Stop();
            if (creatureSFX != null) creatureSFX.Stop();
            if (tentacleSFX != null) tentacleSFX.Stop();
            if (breathingSFX != null) breathingSFX.Stop();
            if (footstepSFX != null) footstepSFX.Stop();
        }
    }
}
