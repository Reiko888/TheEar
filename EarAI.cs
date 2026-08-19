using Dusk;
using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication.Generated;
using UnityEngine;

namespace TheEar
{
    public class EarAI : EnemyAI , INoiseListener
    {
        public AudioSource voiceSFX;
        public AudioSource creatureSFX;
        public AudioSource footstepSFX;

        public AudioClip[] distantScreaming;
        public AudioClip[] alertedScreaming;
        public AudioClip[] wanderingNoises;

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
        public float noiseApproximation = 14f;
        private Vector3 previousPosition;
        private Vector2 lastHeardNoisePosition;
        private bool hasStartedKillAnim = false;
        private float killAnimTime;


        //Behaviour States
        //0 - Calm
        //1 - Suspicious
        //2 - Enraged
        //3 - Executing

        public override void Start()
        {
            base.Start();
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            enemyHP = earHealth;
            Debug.Log("Ear spawned. Starting in searching.");
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
        }

        public override void Update()
        {
            base.Update();
            if (isEnemyDead) return;


            hearNoiseCooldown -= Time.deltaTime;

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
                    creatureAnimator.SetBool("isStunned", true);
                    //Break kill animation
                    //Check if state is calm, if so then enrage
                }
            }
            else
            {
                if (creatureAnimator != null)
                {
                    creatureAnimator.SetBool("isStunned", false);
                }
            }

        }

        //helpers






        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            enemyHP -= force;
            if (base.IsOwner)
            {
                if (enemyHP <= 0)
                {
                    KillEnemyOnOwnerClient(); //This is called in EnemyAI, creatureAnimator.SetBool("Dead", value: true); use it in animator
                    return;
                }
                if (inSpecialAnimation)
                {
                    //Stop execution sequence (server rpc)
                }
            }
            if (playerWhoHit !=null)
            {
                //enrage at that target
            }

        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other, hasStartedKillAnim);
            if (!hasStartedKillAnim)
            {
                if (currentBehaviourStateIndex == 3)
                {
                    playerControllerB.inAnimationWithEnemy = this;
                    hasStartedKillAnim = true;
                    if (killAnimTime>=5f)
                    {
                        playerControllerB.DamagePlayer(100, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling);
                    }
                }

            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void KillFinishedServerRpc(int playerId)
        {
            if (!hasStartedKillAnim)
            {
                return;
            }
            else
            {
                hasStartedKillAnim = false;
            }

        }

        [ClientRpc]
        public void CancelKillAnimationWithPlayerClientRpc(int playerObjectId)
        {
            StartOfRound.Instance.allPlayerScripts[playerObjectId].inAnimationWithEnemy = null;
        }


    }
}
