using UnityEngine;

namespace TheEar
{
    public class AudioAnimationEventPlus : MonoBehaviour
    {
        public AudioSource[] audioSources;
        public AudioClip[] audioClips;

        [System.Serializable]
        public struct RandomClipPool
        {
            public AudioClip[] clips;
        }
        public RandomClipPool[] randomClipPools;

        public bool randomizePitch;
        public float minPitch = 0.8f;
        public float maxPitch = 1.2f;
        public bool playAudibleNoise = true;

        private AudioSource GetSource(int sourceIndex)
        {
            if (audioSources == null || audioSources.Length == 0) return null;
            int idx = Mathf.Clamp(sourceIndex, 0, audioSources.Length - 1);
            return audioSources[idx];
        }

        public void PlayClip(int clipIndex)
        {
            PlayClipOnSource(clipIndex, 0);
        }

        public void PlayClipString(string eventData)
        {
            string[] parts = eventData.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int clipIdx) && int.TryParse(parts[1], out int sourceIdx))
            {
                PlayClipOnSource(clipIdx, sourceIdx);
            }
        }

        public void PlayClipOnSource(int clipIndex, int sourceIndex)
        {
            if (audioClips == null || clipIndex >= audioClips.Length || audioClips[clipIndex] == null) return;
            AudioSource source = GetSource(sourceIndex);
            if (source == null) return;

            if (randomizePitch)
            {
                source.pitch = Random.Range(minPitch, maxPitch);
            }

            source.PlayOneShot(audioClips[clipIndex]);
            WalkieTalkie.TransmitOneShotAudio(source, audioClips[clipIndex]);

            if (playAudibleNoise)
            {
                RoundManager.Instance.PlayAudibleNoise(transform.position, 10f, 0.65f, 0, false, 546);
            }
        }

        public void PlayRandomClipFromPool(int poolIndex)
        {
            PlayRandomClipFromPoolOnSource(poolIndex, 0);
        }

        public void PlayRandomClipFromPoolString(string eventData)
        {
            string[] parts = eventData.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int poolIdx) && int.TryParse(parts[1], out int sourceIdx))
            {
                PlayRandomClipFromPoolOnSource(poolIdx, sourceIdx);
            }
        }

        public void PlayRandomClipFromPoolOnSource(int poolIndex, int sourceIndex)
        {
            if (randomClipPools == null || poolIndex >= randomClipPools.Length) return;
            var pool = randomClipPools[poolIndex].clips;
            if (pool == null || pool.Length == 0) return;

            AudioClip clip = pool[Random.Range(0, pool.Length)];
            if (clip == null) return;

            AudioSource source = GetSource(sourceIndex);
            if (source == null) return;

            if (randomizePitch)
            {
                source.pitch = Random.Range(minPitch, maxPitch);
            }

            source.PlayOneShot(clip);
            WalkieTalkie.TransmitOneShotAudio(source, clip);

            if (playAudibleNoise)
            {
                RoundManager.Instance.PlayAudibleNoise(transform.position, 10f, 0.65f, 0, false, 546);
            }
        }
    }
}
