using Dusk;
using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
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

        private System.Random enemyRandom;

        public override void Start()
        {
            base.Start();
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
        }

        public override void Update()
        {
            base.Update();
        }
    }
}
