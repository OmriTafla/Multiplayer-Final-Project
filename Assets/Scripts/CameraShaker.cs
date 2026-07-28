#if DOTWEEN
using DG.Tweening;
using Singleton;
using UnityEngine;

namespace Abb2kTools
{
    [System.Serializable]
    public class CamShake
    {
        public float duration;
        public Vector3 strength = Vector3.zero;
        public int vibrato = 10;
        public float randomness = 90;
        public bool fadeOut = true;
        public ShakeRandomnessMode randomnessMode;
    }

    [System.Serializable]
    public class CamShakeData
    {
        public CamShake positionShake;
        public CamShake rotationShake;
    }

    public class CameraShaker : Singleton<CameraShaker>
    {
        private Camera _camera;
        private Camera Cam
        {
            get {
                if (_camera == null) _camera = Camera.main;
                return _camera;
            }
        }

        private Tweener positionShake;
        private Tweener rotationShake;

        private Transform _dummyShakeTarget;
        private Transform DummyShakeTarget
        {
            get
            {
                if (_dummyShakeTarget == null)
                {
                    GameObject go = new GameObject("[CameraShaker_DummyTarget]");
                    go.transform.SetParent(transform);
                    _dummyShakeTarget = go.transform;
                }
                return _dummyShakeTarget;
            }
        }

        private Vector3 cleanPos;
        private Quaternion cleanRot;
        private bool isShakeApplied = false;

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
            RestoreCleanPosition();
        }

        private void Update() => RestoreCleanPosition();
        private void FixedUpdate() => RestoreCleanPosition();

        private void RestoreCleanPosition()
        {
            if (isShakeApplied && Cam != null)
            {
                Cam.transform.position = cleanPos;
                Cam.transform.rotation = cleanRot;
                isShakeApplied = false;
            }
        }

        public void Shake(CamShakeData shake)
        {
            if (Cam == null) return;

            DummyShakeTarget.localPosition = Vector3.zero;
            DummyShakeTarget.localRotation = Quaternion.identity;

            if (shake.positionShake != null && shake.positionShake.duration > 0)
            {
                if (positionShake != null) positionShake.Complete();

                positionShake = DummyShakeTarget.DOShakePosition(
                    duration: shake.positionShake.duration,
                    strength: shake.positionShake.strength,
                    vibrato: shake.positionShake.vibrato,
                    randomness: shake.positionShake.randomness,
                    snapping: false,
                    fadeOut: shake.positionShake.fadeOut,
                    randomnessMode: shake.positionShake.randomnessMode
                );
            }
            
            if (shake.rotationShake != null && shake.rotationShake.duration > 0)
            {
                if (rotationShake != null) rotationShake.Complete();

                rotationShake = DummyShakeTarget.DOShakeRotation(
                    duration: shake.rotationShake.duration,
                    strength: shake.rotationShake.strength,
                    vibrato: shake.rotationShake.vibrato,
                    randomness: shake.rotationShake.randomness,
                    fadeOut: shake.rotationShake.fadeOut,
                    randomnessMode: shake.rotationShake.randomnessMode
                );
            }
        }

        private void OnBeforeRender()
        {
            if (Cam == null) return;

            bool isPosActive = positionShake != null && positionShake.IsActive();
            bool isRotActive = rotationShake != null && rotationShake.IsActive();

            if (!isPosActive && !isRotActive) return;

            if (!isShakeApplied)
            {
                cleanPos = Cam.transform.position;
                cleanRot = Cam.transform.rotation;
            }

            Vector3 posOffset = isPosActive ? (cleanRot * DummyShakeTarget.localPosition) : Vector3.zero;
            Quaternion rotOffset = isRotActive ? DummyShakeTarget.localRotation : Quaternion.identity;

            Cam.transform.position = cleanPos + posOffset;
            Cam.transform.rotation = cleanRot * rotOffset;

            isShakeApplied = true;
        }
    }
}
#endif