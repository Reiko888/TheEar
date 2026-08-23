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
        public AudioClip[] enragedScreams;
        public AudioClip[] doorSlam;

        public GameObject tentacleL1Drop;
        public GameObject tentacleL2Drop;
        public GameObject tentacleRDrop;
        public GameObject tentacleMouthStink;
        public GameObject tentacleMouthStink2;
        public GameObject tentacleMouthStink3;
        public GameObject earCanalGunk;

        private int earHealth;
        private float wanderSpeed;
        private float investigateSpeed;
        private float dashSpeed;

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
        private float debugLogTimer = 1f;
        private PlayerControllerB targetPlayerHolder;
        private Coroutine killPlayerCoroutine;
        private bool isEnraged = false;
        private float suspicionTimer;
        private RoundManager roundManager;
        private float lastHeardNoiseDistanceWhenHeard;
        private float wanderNoiseTimer;
        private float timeSinceHittingOtherEnemy;
        private float noiseOverwhelmAccumulator;
        private float overwhelmTimer;
        private bool hasScreamedOverwhelmed;
        private DoorLock[] cachedDoors;
        private float breakDoorTimer;
        private float slowTimer;
        private int previousState;
        private float hearingRangeMultiplier;
        private float wanderSearchWidth = 400f;
        private float wanderSearchPrecision = 15f;
        private float voiceOverwhelmMultiplier;
        private float normalOverwhelmMultiplier;
        private float voiceLoudnessThreshold = 0.5f;
        private float normalLoudnessThreshold = 1.5f;
        private float killCooldown;
        private float attackTransitionCooldown;
        private bool serverIsAttackingOrGrabbing;
        private bool isExecutingKill;
        private bool wasStunned;

        //Behaviour States
        //0 - Calm
        //1 - Suspicious
        //2 - Enraged
        //3 - Executing

        public override void Start()
        {
            base.Start();
            earHealth = EarContentHandler.Instance.earAssets.GetConfig<int>("Ear health").Value;
            wanderSpeed = EarContentHandler.Instance.earAssets.GetConfig<float>("Wander speed").Value;
            investigateSpeed = EarContentHandler.Instance.earAssets.GetConfig<float>("Investigating speed").Value;
            dashSpeed = EarContentHandler.Instance.earAssets.GetConfig<float>("Frenzied speed").Value;
            hearingRangeMultiplier = EarContentHandler.Instance.earAssets.GetConfig<float>("Hearing range multiplier").Value;
            voiceOverwhelmMultiplier = EarContentHandler.Instance.earAssets.GetConfig<float>("Voice overwhelm multiplier").Value;
            normalOverwhelmMultiplier = EarContentHandler.Instance.earAssets.GetConfig<float>("Normal overwhelm multiplier").Value;

            previousState = currentBehaviourStateIndex;
            if (wanderFacility != null) //customise search routine
            {
                wanderFacility.searchWidth = wanderSearchWidth;
                wanderFacility.searchPrecision = wanderSearchPrecision;
            }
            cachedDoors = UnityEngine.Object.FindObjectsOfType<DoorLock>(); //cache doors once due to expensive findobjectsoftype (would be expensive scanning with this in update).
            roundManager = UnityEngine.Object.FindObjectOfType<RoundManager>();
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            enemyHP = earHealth;
            breathingSFX.clip = calmerBreathing; //Select the calm breathing clip as monster spawns calm.
            breathingSFX.Play();
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
        }

        public override void Update()
        {
            base.Update();
            if (isEnemyDead) return;
            if (killCooldown > 0f)
            {
                killCooldown -= Time.deltaTime;
            }
            if (previousState == 2 && currentBehaviourStateIndex != 2)
            {
                if (enemyBehaviourStates != null && enemyBehaviourStates.Length > 2 && enemyBehaviourStates[2].IsAnimTrigger)
                {
                    creatureAnimator.ResetTrigger(enemyBehaviourStates[2].parameterString); //To avoid animation triggers being backlogged and triggering when unwanted.
                }
            }
            if (previousState == 0 && (currentBehaviourStateIndex == 1 || currentBehaviourStateIndex == 2)) //We only want the gunk playing when tentacle is released.
            {
                if (earCanalGunk != null)
                {
                    ParticleSystem[] particles = earCanalGunk.GetComponentsInChildren<ParticleSystem>(); //Play ALL particle systems
                    for (int i = 0; i < particles.Length; i++)
                    {
                        particles[i].Play();
                    }
                }
            }
            previousState = currentBehaviourStateIndex;
            if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController != null && !GameNetworkManager.Instance.localPlayerController.isPlayerDead)
            {
                if (currentBehaviourStateIndex == 2) //Only in enraged
                {
                    if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(transform.position, 50f, 25, 10f))
                    {
                        GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1f); //Max fear
                    }
                }
                else
                {
                    if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(transform.position, 50f, 30, 5f))
                    {
                        GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.25f, 0.3f); //moderate fear
                    }
                }
            }
            timeSinceHittingOtherEnemy += Time.deltaTime;
            if (breakDoorTimer > 0f)
            {
                breakDoorTimer -= Time.deltaTime;
            }
            if (slowTimer > 0f)
            {
                slowTimer -= Time.deltaTime;
            }
            noiseOverwhelmAccumulator = Mathf.Max(0f, noiseOverwhelmAccumulator - Time.deltaTime);
            if (overwhelmTimer > 0f)
            {
                overwhelmTimer -= Time.deltaTime;
                if (!hasScreamedOverwhelmed)
                {
                    hasScreamedOverwhelmed = true;
                    creatureVoice.PlayOneShot(enragedScreams[enemyRandom.Next(0, enragedScreams.Length)]);
                }
                creatureAnimator.SetBool("isOverwhelmed", true);
            }
            else
            {
                creatureAnimator.SetBool("isOverwhelmed", false);
            }
            debugLogTimer -= Time.deltaTime;
            if (debugLogTimer <= 0f)
            {
                debugLogTimer = 1f;
                //Debug.Log("EAR: State=" + currentBehaviourStateIndex + " Owner=" + base.IsOwner + " Suspicion=" + suspicionLevel + " Timer=" + suspicionTimer + " OverwhelmAcc=" + noiseOverwhelmAccumulator + " IsOverwhelmed=" + (overwhelmTimer > 0f));
            }
            if (currentBehaviourStateIndex == 2 || currentBehaviourStateIndex == 3) //In enraged and execution
            {
                if (breathingSFX.clip != frenziedBreathing)
                {
                    breathingSFX.clip = frenziedBreathing;
                    breathingSFX.Play();
                }
            }
            else
            {
                if (breathingSFX.clip != calmerBreathing)
                {
                    breathingSFX.clip = calmerBreathing;
                    breathingSFX.Play();
                }
            }

            bool shouldEnableDrops = (currentBehaviourStateIndex == 2 || currentBehaviourStateIndex == 3);
            if (tentacleL1Drop.activeSelf != shouldEnableDrops) tentacleL1Drop.SetActive(shouldEnableDrops);
            if (tentacleL2Drop.activeSelf != shouldEnableDrops) tentacleL2Drop.SetActive(shouldEnableDrops);
            if (tentacleRDrop.activeSelf != shouldEnableDrops) tentacleRDrop.SetActive(shouldEnableDrops);
            bool shouldEnableStink = (currentBehaviourStateIndex == 3);
            if (tentacleMouthStink.activeSelf != shouldEnableStink) tentacleMouthStink.SetActive(shouldEnableStink);
            if (tentacleMouthStink2.activeSelf != shouldEnableStink) tentacleMouthStink2.SetActive(shouldEnableStink);
            if (tentacleMouthStink3.activeSelf != shouldEnableStink) tentacleMouthStink3.SetActive(shouldEnableStink);


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
                wasStunned = true;
                agent.speed = 0f;
                if (creatureAnimator != null)
                {
                    creatureAnimator.SetBool("stunned", true);
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
                    creatureAnimator.SetBool("stunned", false);
                }
                if (wasStunned)
                {
                    wasStunned = false;
                    if (base.IsOwner)
                    {
                        if (suspicionLevel <= 1)
                        {
                            SwitchToBehaviourState(0);
                        }
                        else if (suspicionLevel <= 8)
                        {
                            string state2Param = enemyBehaviourStates[2].parameterString;
                            if (enemyBehaviourStates[2].IsAnimTrigger)
                            {
                                creatureAnimator.ResetTrigger(state2Param);
                            }
                            SwitchToBehaviourState(1);
                        }
                        else
                        {
                            SwitchToBehaviourState(2);
                        }
                    }
                }
            }
            if (currentBehaviourStateIndex == 3 || inSpecialAnimation)
            {
                if (base.IsOwner)
                {
                    agent.speed = 0f;
                    agent.velocity = Vector3.zero;
                }
            }

            switch (currentBehaviourStateIndex)
            {
                case 0:
                    if (base.IsOwner)
                    {
                        if (stunNormalizedTimer <= 0f)
                        {
                            agent.speed = wanderSpeed;
                        }
                        else
                        {
                            agent.speed = 0f;
                            agent.velocity = Vector3.zero;
                        }
                        if (base.IsOwner && !wanderFacility.inProgress)
                        {
                            StartSearch(base.transform.position, wanderFacility);
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
                    if (stunNormalizedTimer <= 0f)
                    {
                        agent.speed = investigateSpeed;
                    }
                    else
                    {
                        agent.speed = 0f;
                        agent.velocity = Vector3.zero;
                    }
                    if (!agent.pathPending && Vector3.Distance(base.transform.position, agent.destination) < 3f)
                    {
                        SearchForPreviouslyHeardSound();
                    }
                    suspicionTimer -= Time.deltaTime;
                    if (suspicionTimer <= 0f)
                    {
                        suspicionTimer = 4f;
                        suspicionLevel--;
                        SyncSuspicionServerRpc(suspicionLevel);
                        if (suspicionLevel <= 1)
                        {
                            SwitchToBehaviourState(0);
                            if (base.IsOwner && !IsServer)
                            {
                                ChangeOwnershipOfEnemy(0);
                            }
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
                    if (stunNormalizedTimer <= 0f)
                    {
                        agent.speed = dashSpeed;
                    }
                    else
                    {
                        agent.speed = 0f;
                        agent.velocity = Vector3.zero;
                    }
                    if (breakDoorTimer <= 0f && cachedDoors != null)
                    {
                        foreach (DoorLock door in cachedDoors) //loop through cached doors
                        {
                            if (door == null) continue;
                            AnimatedObjectTrigger animTrigger = door.GetComponent<AnimatedObjectTrigger>();
                            if (animTrigger != null && animTrigger.boolValue && !door.isLocked)
                            {
                                continue;
                            }
                            GameObject doorObj = door.transform.parent.transform.parent.transform.parent.gameObject;
                            if (!doorObj.GetComponent<Rigidbody>() && Vector3.Distance(transform.position, doorObj.transform.position) <= 4f)
                            {
                                breakDoorTimer = 1f;
                                creatureAnimator.SetTrigger("breakDoor");
                                Vector3 pushForce = (doorObj.transform.position - transform.position).normalized * 20f;
                                BreakDoorServerRpc(doorObj, pushForce);
                                break;
                            }
                        }
                    }
                    if (!agent.pathPending && Vector3.Distance(base.transform.position, agent.destination) < 3f)
                    {
                        SearchForPreviouslyHeardSound();
                    }
                    suspicionTimer -= Time.deltaTime;
                    if (suspicionTimer <= 0f)
                    {
                        suspicionTimer = 3f;
                        suspicionLevel--;
                        SyncSuspicionServerRpc(suspicionLevel);
                        if (suspicionLevel <= 8)
                        {
                            string state2Param = enemyBehaviourStates[2].parameterString;
                            if (enemyBehaviourStates[2].IsAnimTrigger)
                            {
                                creatureAnimator.ResetTrigger(state2Param);
                            }
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
            if (currentBehaviourStateIndex == 0 || currentBehaviourStateIndex == 1)
            {
                wanderNoiseTimer -= Time.deltaTime;
                if (wanderNoiseTimer <= 0f)
                {
                    wanderNoiseTimer = UnityEngine.Random.Range(8f, 15f);
                    creatureSFX.PlayOneShot(wanderingNoises[enemyRandom.Next(0, wanderingNoises.Length)]);
                }
            }
            if (slowTimer > 0f)
            {
                agent.speed = 0.2f;
            }
            if (breakDoorTimer > 0f)
            {
                agent.speed = 0f;
            }
            if (overwhelmTimer > 0f)
            {
                agent.speed = 0.5f;
            }
            if (attackTransitionCooldown > 0f)
            {
                attackTransitionCooldown -= Time.deltaTime;
                if (base.IsOwner)
                {
                    agent.speed = 0f;
                    agent.velocity = Vector3.zero;
                }
            }
            }

        //helpers

        private void SearchForPreviouslyHeardSound()
        {
            int num = 0;
            Vector3 vector = base.transform.position;
            while (num < 5 && Vector3.Distance(vector, base.transform.position) < 8f)
            {
                num++;
                vector = roundManager.GetRandomNavMeshPositionInRadius(lastHeardNoisePosition, 18f);
            }
            SetDestinationToPosition(vector);
            noisePositionGuess = vector;
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesNoisePlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);

            if (isEnemyDead || stunNormalizedTimer > 0f || hearNoiseCooldown > 0f || hasStartedKillAnim || noiseID == 546 || noiseID == 7 || timesNoisePlayedInOneSpot > 15)
            {
                return;
            }
            hearNoiseCooldown = 0.03f;
            float distance = Vector3.Distance(transform.position, noisePosition); //Distance between the ear instance and the noise
            float hearRange = hearingRangeMultiplier * noiseLoudness; //Mulitplier range by the loudness to scale reaction
            if (Physics.Linecast(transform.position, noisePosition, 256))
            {
                noiseLoudness /= 2f;
                hearRange /= 2f;
            }
            if (noiseLoudness < 0.25f)
            {
                return; //Ignore tiny noises
            }
            if (overwhelmTimer <= 0f)
            {
                if (noiseID == 75) //75 is ID for voice chat audio
                {
                    if (noiseLoudness >= voiceLoudnessThreshold)
                    {
                        noiseOverwhelmAccumulator += noiseLoudness * voiceOverwhelmMultiplier; //Multiply overwhlem accumulation when hearing voices by config value
                    }
                }
                else
                {
                    if (noiseLoudness >= normalLoudnessThreshold) //Multiply overwhlem accumulation when hearing non-voice noises by config value
                    {
                        noiseOverwhelmAccumulator += noiseLoudness * normalOverwhelmMultiplier;
                    }
                }
                if (noiseOverwhelmAccumulator >= 5f)
                {
                    noiseOverwhelmAccumulator = 0f; //reset
                    TriggerOverwhelmServerRpc();
                }
            }
            if (distance < hearRange)
            {
                if (!base.IsOwner)
                {
                    ChangeOwnershipOfEnemy(NetworkManager.Singleton.LocalClientId); //If Ear on client hears noise, set ownership to noise maker if they are not already owner.
                }
                if (currentBehaviourStateIndex < 2)
                {
                    suspicionLevel = 9;
                }
                else
                {
                    suspicionLevel++;
                }
                suspicionLevel = Mathf.Min(suspicionLevel, 10);
                SyncSuspicionServerRpc(suspicionLevel);
                Vector3 guessedPos = noisePosition; //Guess position
                if (distance > 2f)
                {
                    guessedPos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(noisePosition, distance / noiseApproximation); //get position on Nav Mesh.
                }
                SyncNoiseTargetServerRpc(guessedPos, distance);
            }
        }

        private void ApplyNoiseAlert(Vector3 targetPosition, float distanceToNoise)
        {
            if (distanceToNoise < 8f)
            {
                if (currentBehaviourStateIndex < 2)
                {
                    creatureVoice.PlayOneShot(enragedScreams[enemyRandom.Next(0, enragedScreams.Length)]);
                    creatureSFX.PlayOneShot(alertedScreaming[enemyRandom.Next(0, alertedScreaming.Length)]);
                    distantSFX.PlayOneShot(distantScreaming[enemyRandom.Next(0, distantScreaming.Length)]);
                    SwitchToBehaviourState(2);
                    suspicionLevel = 10;
                    SyncSuspicionServerRpc(suspicionLevel);
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
                    creatureSFX.PlayOneShot(alertedScreaming[enemyRandom.Next(0, alertedScreaming.Length)]);
                    distantSFX.PlayOneShot(distantScreaming[enemyRandom.Next(0, distantScreaming.Length)]);
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
            if (inSpecialAnimation && playerWhoHit == targetPlayerHolder)
            {
                return; //player in anim cannoy damage enemy
            }
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            enemyHP -= force;
            creatureSFX.PlayOneShot(enemyType.hitBodySFX);
            creatureVoice.PlayOneShot(enemyType.hitEnemyVoiceSFX);

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
                if (inSpecialAnimation && playerWhoHit != targetPlayerHolder)
                {
                    StopKillAnimationServerRpc(playerWhoHit != null ? (int)playerWhoHit.playerClientId : -1);
                }
            }
            if (playerWhoHit != null && IsServer)
            {
                targetPlayer = playerWhoHit;
            }
        }

        public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1f, PlayerControllerB setStunnedByPlayer = null)
        {
            base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
            if (isEnemyDead || !enemyType.canBeStunned) return;

            if (setToStunned && postStunInvincibilityTimer <= 0f)
            {
                creatureVoice.PlayOneShot(enemyType.stunSFX);

                if (base.IsOwner)
                {
                    agent.speed = 0f;
                    agent.velocity = Vector3.zero;
                }
            }
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            if (overwhelmTimer > 0f) return; //Cant hit when overwhelmed
            if (!base.IsOwner) return;
            if (killCooldown > 0f || isExecutingKill) return;
            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other, hasStartedKillAnim);
            //Debug.Log("EAR: Collision detected.");
            if (playerControllerB != null && !hasStartedKillAnim && !inSpecialAnimation && !isExecutingKill)
            {
                if (currentBehaviourStateIndex == 2)
                {
                    GrabPlayerServerRpc((int)playerControllerB.playerClientId);
                }
            }
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
        {
            base.OnCollideWithEnemy(other, collidedEnemy);
            if (overwhelmTimer > 0f) return;
            if (collidedEnemy != null && collidedEnemy.enemyType != enemyType && !collidedEnemy.isEnemyDead && currentBehaviourStateIndex == 2)
            {
                slowTimer = 1f; //Slow down duration
                if (collidedEnemy.enemyType.canDie && timeSinceHittingOtherEnemy >= 1f)
                {
                    timeSinceHittingOtherEnemy = 0f;
                    creatureAnimator.SetTrigger("attack");
                    collidedEnemy.HitEnemy(2, null, playHitSFX: true);
                    TriggerLocalCameraShake();
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void GrabPlayerServerRpc(int playerId)
        {
            Debug.Log($"EAR !!SERVER!!: Player grab  called playerId={playerId}, Atttack or grab?={serverIsAttackingOrGrabbing}, hasStartedKillAnim={hasStartedKillAnim}");
            if (serverIsAttackingOrGrabbing || hasStartedKillAnim || isExecutingKill)
            {
                Debug.Log("EAR !!SERVER!!: G rab player early return due to active guard.");
                return;
            }
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];
            if (player == null || player.isPlayerDead)
            {
                Debug.Log($"EAR !!SERVER!!: Grab player early return. player is null or dead. isPlayerDead={player?.isPlayerDead}");
                return;
            }
            Debug.Log($"EAR !!SERVER!! Checking player health on server. playerHealth={player.health}");
            if (player.health < 80)
            {
                serverIsAttackingOrGrabbing = true;
                Debug.Log("EAR !!SERVER!!: Health is below 80. Triggering tentacle slap attack");
                TriggerAttackClientRpc(playerId);
                StartCoroutine(ResetKillAnimFlagAfterDelay(3f));
            }
            else
            {
                serverIsAttackingOrGrabbing = true;
                hasStartedKillAnim = true;
                isExecutingKill = true;
                Debug.Log("EAR !!SERVER!!: Health is 80 or above. Triggering Grab Execution.");
                GrabPlayerClientRpc(playerId);
            }
        }

        private IEnumerator ResetKillAnimFlagAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            serverIsAttackingOrGrabbing = false;
            hasStartedKillAnim = false;
        }

        [ClientRpc]
        public void TriggerAttackClientRpc(int playerId)
        {
            Debug.Log($"!!EAR Client!!: Trigger attack called. IsOwner={base.IsOwner}");
            creatureAnimator.SetTrigger("attack");
            TriggerLocalCameraShake();
            killCooldown = 3f;
            attackTransitionCooldown = 1.5f;
            PlayerControllerB victim = StartOfRound.Instance.allPlayerScripts[playerId];
            if (victim == GameNetworkManager.Instance.localPlayerController)
            {
                victim.DamagePlayer(100, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling, 0);
            }
            if (base.IsOwner && !IsServer)
            {
                ChangeOwnershipOfEnemy(0); //Give ownership back to host
            }
        }

        [ClientRpc]
        public void GrabPlayerClientRpc(int playerId)
        {
            Debug.Log($"!!EAR Client!!:Grab player called. playerId={playerId}, IsOwner={base.IsOwner}");
            if (killPlayerCoroutine != null)
            {
                StopCoroutine(killPlayerCoroutine);
            }
            Debug.Log("Grabbed player!");
            targetPlayerHolder = StartOfRound.Instance.allPlayerScripts[playerId];
            targetPlayerHolder.inAnimationWithEnemy = this;
            inSpecialAnimation = true;
            hasStartedKillAnim = true;
            isExecutingKill = true;
            killCooldown = 3f;
            creatureAnimator.SetBool("isExecuting", true);
            if (base.IsOwner)
            {
                SwitchToBehaviourState(3);
            }
            killPlayerCoroutine = StartCoroutine(executionRoutine(targetPlayerHolder));
        }

        private IEnumerator executionRoutine(PlayerControllerB targetPlayer)
        {
            if (base.IsOwner)
            {
                agent.speed = 0f;
            }
            if (GameNetworkManager.Instance.localPlayerController == targetPlayer)
            {
                targetPlayer.DamagePlayer(75, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling, 1); //To avoid players swapping places.
            }
            targetPlayer.JumpToFearLevel(1f);
            float duration = 5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (base.IsOwner)
                {
                    agent.speed = 0f;
                    agent.velocity = Vector3.zero;
                }
                targetPlayer.JumpToFearLevel(1f);
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
                if (!hasStartedKillAnim || !isExecutingKill || isEnemyDead)
                {
                    yield break;
                }
                float pullProgress = Mathf.Clamp01(elapsed / 3f);
                float currentDistance = Mathf.Lerp(2.0f, 0.0f, pullProgress);
                Vector3 targetPos = torsoPos.position + transform.forward * currentDistance;
                targetPos.y = Mathf.Max(targetPos.y, transform.position.y); //lock y to be in front of ear
                targetPlayer.transform.position = targetPos;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (IsServer)
            {
                KillFinishedServerRpc((int)targetPlayer.playerClientId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void KillFinishedServerRpc(int playerId)
        {
            if (!hasStartedKillAnim && !isExecutingKill)
            {
                return;
            }
            KillFinishedClientRpc(playerId);
        }

        [ClientRpc]
        public void KillFinishedClientRpc(int playerId)
        {
            if (playerId >= 0 && playerId < StartOfRound.Instance.allPlayerScripts.Length)
            {
                PlayerControllerB victim = StartOfRound.Instance.allPlayerScripts[playerId];
                if (victim != null && victim == GameNetworkManager.Instance.localPlayerController && !victim.isPlayerDead)
                {
                    victim.DamagePlayer(100, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling, 1);
                }
            }
            if (killPlayerCoroutine != null)
            {
                StopCoroutine(killPlayerCoroutine);
            }
            hasStartedKillAnim = false;
            inSpecialAnimation = false;
            serverIsAttackingOrGrabbing = false;
            isExecutingKill = false;
            hearNoiseCooldown = 2f;
            killCooldown = 3f;
            Debug.Log("EAR: Kill finished.");
            if (targetPlayerHolder != null)
            {
                targetPlayerHolder.inAnimationWithEnemy = null;
                targetPlayerHolder = null;
            }
            creatureAnimator.SetBool("isExecuting", false);
            Debug.Log($"!!EAR Client!!: Finished kill called. IsOwner={base.IsOwner}, suspicionLevel={suspicionLevel}");
            if (base.IsOwner && !IsServer)
            {
                ChangeOwnershipOfEnemy(0);
            }
            if (suspicionLevel <= 1)
            {
                SwitchToBehaviourState(0);
            }
            else if (suspicionLevel <= 8)
            {
                string state2Param = enemyBehaviourStates[2].parameterString;
                if (enemyBehaviourStates[2].IsAnimTrigger)
                {
                    creatureAnimator.ResetTrigger(state2Param);
                }
                SwitchToBehaviourState(1);
            }
            else
            {
                SwitchToBehaviourState(2);
            }
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
            serverIsAttackingOrGrabbing = false;
            isExecutingKill = false;
            killCooldown = 3f;
            creatureAnimator.SetBool("isExecuting", false);

            if (hitterId != -1)
            {
                PlayerControllerB hitter = StartOfRound.Instance.allPlayerScripts[hitterId];
                if (hitter != null && IsServer)
                {
                    targetPlayer = hitter;
                }
            }
            Debug.Log($"!!EAR Client!!t: Stop kiill anim called. IsOwner={base.IsOwner}, suspicionLevel={suspicionLevel}");
            if (base.IsOwner && !IsServer)
            {
                ChangeOwnershipOfEnemy(0);
            }
            if (suspicionLevel <= 1)
            {
                SwitchToBehaviourState(0);
            }
            else if (suspicionLevel <= 8)
            {
                string state2Param = enemyBehaviourStates[2].parameterString;
                if (enemyBehaviourStates[2].IsAnimTrigger)
                {
                    creatureAnimator.ResetTrigger(state2Param);
                }
                SwitchToBehaviourState(1);
            }
            else
            {
                SwitchToBehaviourState(2);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SyncSuspicionServerRpc(int level)
        {
            SyncSuspicionClientRpc(level);
        }

        [ClientRpc]
        public void SyncSuspicionClientRpc(int level)
        {
            if (!base.IsOwner)
            {
                suspicionLevel = level;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SyncNoiseTargetServerRpc(Vector3 targetPosition, float distanceToNoise)
        {
            SyncNoiseTargetClientRpc(targetPosition, distanceToNoise);
        }

        [ClientRpc]
        public void SyncNoiseTargetClientRpc(Vector3 targetPosition, float distanceToNoise)
        {
            noisePositionGuess = targetPosition;
            lastHeardNoisePosition = targetPosition;
            lastHeardNoiseDistanceWhenHeard = distanceToNoise;
            ApplyNoiseAlert(targetPosition, distanceToNoise);
        }

        [ServerRpc(RequireOwnership = false)]
        public void TriggerOverwhelmServerRpc()
        {
            TriggerOverwhelmClientRpc();
        }

        [ClientRpc]
        public void TriggerOverwhelmClientRpc()
        {
            overwhelmTimer = 4f;
            hasScreamedOverwhelmed = false;
        }

        [ClientRpc]
        public void CancelKillAnimationWithPlayerClientRpc(int playerObjectId)
        {
            StartOfRound.Instance.allPlayerScripts[playerObjectId].inAnimationWithEnemy = null;
        }

        public override void KillEnemy(bool destroy = false)
        {
            base.KillEnemy(destroy);
            distantSFX.Stop();
            creatureSFX.Stop();
            tentacleSFX.Stop();
            breathingSFX.Stop();
            footstepSFX.Stop();
            tentacleL1Drop.SetActive(false);
            tentacleL2Drop.SetActive(false);
            tentacleRDrop.SetActive(false);
            tentacleMouthStink.SetActive(false);
            tentacleMouthStink2.SetActive(false);
            tentacleMouthStink3.SetActive(false);

            ParticleSystem[] particles = earCanalGunk.GetComponentsInChildren<ParticleSystem>();
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void BreakDoorServerRpc(NetworkObjectReference netObjRef, Vector3 force)
        {
            BreakDoorClientRpc(netObjRef, force);
        }

        //Thanks to The Fiend code!
        [ClientRpc]
        public void BreakDoorClientRpc(NetworkObjectReference netObjRef, Vector3 force)
        {
            if (netObjRef.TryGet(out var networkObject))
            {
                GameObject door = networkObject.gameObject;
                Rigidbody rigidbody = door.AddComponent<Rigidbody>();
                AudioSource audioSource = door.AddComponent<AudioSource>(); //Add an audiosource to the door
                audioSource.PlayOneShot(doorSlam[enemyRandom.Next(0, doorSlam.Length)]); //Play a slam sound
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 60f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.volume = 3f;
                StartCoroutine(TurnOffC(rigidbody, 0.12f)); //Turn of rigidbody so it doesnt fly everywhere
                rigidbody.AddForce(force, ForceMode.Impulse);
            }
        }

        private IEnumerator TurnOffC(Rigidbody rigidbody, float time)
        {
            rigidbody.detectCollisions = false;
            yield return new WaitForSeconds(time);
            rigidbody.detectCollisions = true;
            if (IsServer)
            {
                Destroy(rigidbody.gameObject, 5f); //Destroy door to clean up
            }
        }

        private void TriggerLocalCameraShake()
        {
            if (HUDManager.Instance != null)
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                if (localPlayer != null && !localPlayer.isPlayerDead && Vector3.Distance(transform.position, localPlayer.transform.position) < 15f)
                {
                    HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
                }
            }
        }

    }
}
