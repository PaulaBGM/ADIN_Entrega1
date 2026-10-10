using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace JPS4
{
    /// <summary>
    /// Animation Utils.
    /// Copyright: Silo Interactive 2026
    /// </summary>
    public static class AnimationUtils
    {
        public static void CancelAndDispose(ref CancellationTokenSource actionCts)
        {
            if (actionCts == null) return;
            if (!actionCts.IsCancellationRequested) actionCts.Cancel();
            actionCts.Dispose();
            actionCts = null;
        }

        public static void CancelAndNew(ref CancellationTokenSource actionCts)
        {
            CancelAndDispose(ref actionCts);
            actionCts = new CancellationTokenSource();
        }

        public static void CancelAndNew(ref CancellationTokenSource actionCts, MonoBehaviour script)
        {
            CancelAndNew(ref actionCts);
            actionCts = CancellationTokenSource.CreateLinkedTokenSource(actionCts.Token,
                script.destroyCancellationToken);
        }

        public static void CancelAndNew(ref CancellationTokenSource actionCts, MonoBehaviour script,
            CancellationToken ct)
        {
            CancelAndNew(ref actionCts);
            actionCts = CancellationTokenSource.CreateLinkedTokenSource(actionCts.Token,
                script.destroyCancellationToken, ct);
        }

        public static void TryCancel(ref CancellationTokenSource internalCts)
        {
            if (internalCts == null || internalCts.IsCancellationRequested) return;
            internalCts.Cancel();
        }
        
        public static void LocalPositionLerper(this Transform transform, Vector3 x) => transform.localPosition = x;
        public static void PositionLerper(this Transform transform, Vector3 x) => transform.position = x;
        public static void LocalScaleLerper(this Transform transform, Vector3 x) => transform.localScale = x;

        public static void UniformLocalScaleLerper(this Transform transform, float x) => transform.localScale = x * Vector3.one;

        public static void RotationLerper(this Transform transform, Quaternion x) => transform.rotation = x;
        public static void LocalRotationLerper(this Transform transform, Quaternion x) => transform.localRotation = x;
        
        
        [System.Serializable]
        public struct AnimData
        {
            public float Dur;
            public AnimationUtils.EAnimationCurve Curve;
        }
        
        [System.Serializable]
        public struct AnimDataValue<T>
        {
            public T Value;
            public float Dur;
            public AnimationUtils.EAnimationCurve Curve;
        }
        
        [System.Serializable]
        public struct AnimDataDelay
        {
            public float Dur;
            public AnimationUtils.EAnimationCurve Curve;
            public float Delay;
        }
        
        [System.Serializable]
        public struct AnchoredAnimData
        {
            public float FromY; // To is zero for anchored
            public float Dur;
            public AnimationUtils.EAnimationCurve Curve;
            public float Delay;
        }
        
        // Animation Curves
        public static readonly Func<float, float> FLinear = (x) => x;
        public static readonly Func<float, float> FEaseInOutCubic = (x) => x < 0.5f ? 4.0f * x * x * x : 1.0f - Mathf.Pow(-2.0f * x + 2.0f, 3.0f) * 0.5f;

        public static readonly Func<float, float> FEaseInSine = (x) => 1f - Mathf.Cos((x * Mathf.PI) * 0.5f);

        public static readonly Func<float, float> FEaseInExpo = (x) => x == 0f ? 0f : Mathf.Pow(2f, 10f * x - 10f);
        
        // Ease out:
        public static readonly Func<float, float> FEaseOutExpo = (x) => x == 1.0f ? 1 : 1 - Mathf.Pow(2.0f, -10.0f * x);
        public static readonly Func<float, float> FEaseOutQuart = (x) => 1.0f - Mathf.Pow(1.0f - x, 4.0f);
        public static readonly Func<float, float> FEaseOutElastic = (x) =>
        {
            return x == 0 ? 0 : x == 1.0f ? 1 : Mathf.Pow(2.0f, -10.0f * x) * Mathf.Sin((float)((x * 10.0f - 0.75f) * C4)) + 1.0f;
        };
        public static readonly Func<float, float> FEaseOutBack = (x) => 1f + C3 * Mathf.Pow(x - 1f, 3) + C1 * Mathf.Pow(x - 1f, 2);
        public static readonly Func<float, float> FEaseOutBackHalf = (x) => 1f + (2.1f) * Mathf.Pow(x - 1f, 3) + (1.1f) * Mathf.Pow(x - 1f, 2);
        public static readonly Func<float, float> FEaseOutCubic = (x) => 1 - Mathf.Pow(1 - x, 3);
        public static readonly Func<float, float> FEaseOutSine = (x) => Mathf.Sin((x * Mathf.PI)*0.5f);
        public static readonly Func<float, float> FEaseOutQuint = (x) => 1 -Mathf.Pow(1 - x, 5);
        public static readonly Func<float, float> FEaseOutCirc = (x) => Mathf.Sqrt(1f - Mathf.Pow(x - 1f, 2f));
        public static readonly Func<float, float> FEaseOutQuad = (x) => 1 - (1 - x) * (1 - x);
        
        // Ease In:
        public static readonly Func<float, float> FEaseInQuad = (x) => x * x;
        public static readonly Func<float, float> FEaseInCubic = (x) => x * x * x;
        public static readonly Func<float, float> FEaseInQuint = (x) => x * x * x * x * x;
        public static readonly Func<float, float> FEaseInCirc = (x) => 1f - Mathf.Sqrt(1f - Mathf.Pow(x, 2f));
        public static readonly Func<float, float> FEaseInBack = (x) => C3 * x * x * x - C1 * x * x;

        
        // There and back Curves
        public static readonly Func<float, float> FEasedSinePulse = (x) =>  Mathf.Pow(Mathf.Sin(x * Mathf.PI), 3); // _/--\__ returns back to start
        
        // Constants
        private const double C4 = (2.0f * Math.PI) / 3.0f;
        private const float C1 = 1.70158f;
        private const float C3 = C1 + 1f;
        
        public enum EAnimationCurve
        {
            Linear = 0,
            
            EaseOutBack = 10, // 1.75
            EaseOutBackHalf, // More subtle ease out back with 1.10 constant
            EaseOutQuart,
            EaseOutExpo,
            EaseOutCubic,
            EaseOutSine,
            EaseOutQuint,
            EaseOutCirc,
            EaseOutQuad,
            EaseOutElastic,
            
            EaseInExpo=30,
            EaseInSine,
            EaseInCubic,
            EaseInQuint,
            EaseInCirc,
            EaseInQuad,
            EaseInBack,
            
            EaseInOutCubic = 50,
            
            EasedSinePulse =80,
        }
        
        private static readonly Dictionary<EAnimationCurve, Func<float,float>> AnimationFuncs = new()
        {
            {EAnimationCurve.Linear, FLinear},
            {EAnimationCurve.EaseInOutCubic, FEaseInOutCubic},
            {EAnimationCurve.EaseInSine, FEaseInSine},
            {EAnimationCurve.EaseInExpo, FEaseInExpo},
            {EAnimationCurve.EasedSinePulse, FEasedSinePulse},

            {EAnimationCurve.EaseOutExpo, FEaseOutExpo},
            {EAnimationCurve.EaseOutQuart, FEaseOutQuart},
            {EAnimationCurve.EaseOutElastic, FEaseOutElastic},
            {EAnimationCurve.EaseOutBack, FEaseOutBack},
            {EAnimationCurve.EaseOutBackHalf, FEaseOutBackHalf},
            {EAnimationCurve.EaseOutCubic, FEaseOutCubic},
            {EAnimationCurve.EaseOutSine, FEaseOutSine},
            {EAnimationCurve.EaseOutQuint, FEaseOutQuint},
            {EAnimationCurve.EaseOutCirc, FEaseOutCirc},
            {EAnimationCurve.EaseOutQuad, FEaseOutQuad},
            
            {EAnimationCurve.EaseInCubic, FEaseInCubic},
            {EAnimationCurve.EaseInQuint, FEaseInQuint},
            {EAnimationCurve.EaseInCirc, FEaseInCirc},
            {EAnimationCurve.EaseInQuad, FEaseInQuad},
            {EAnimationCurve.EaseInBack, FEaseInBack},
        };
        
        public static float Value(this EAnimationCurve lhs, float x) => AnimationFuncs[lhs](x);

        public static IEnumerator InterpAction(Transform transform, Action<Vector3> setter, Vector3 startValue, Vector3 endValue, float duration, EAnimationCurve interpolation, bool unScaled=false, float delay=-1f)
        {
            if (setter == null) yield break;
            if (delay > 0)
            {
                setter(startValue); // Ensure start is set while waiting
                yield return new WaitForSeconds(delay);
            }
            float timeElapsed = 0;
            float durInv = 1.0f / duration;
            while (timeElapsed < duration)
            {
                setter(startValue + interpolation.Value(timeElapsed * durInv) * (endValue - startValue));
                yield return null; // Wait for next frame
                timeElapsed += !unScaled ? Time.deltaTime : Time.unscaledDeltaTime;
            }

            if (transform) setter(endValue); // Can pass null as transform to prevent setting on completion
        }
        
        public static IEnumerator InterpAction(Transform transform, Action<Quaternion> setter, Quaternion startValue, Quaternion endValue, float duration, EAnimationCurve interpolation)
        {
            if (setter == null) yield break;
            float timeElapsed = 0;
            float durInv = 1.0f / duration;
            while (timeElapsed < duration)
            {
                setter(Quaternion.Slerp(startValue, endValue, interpolation.Value(timeElapsed * durInv)));
                yield return null; // Wait for next frame
                timeElapsed += Time.deltaTime;
            }

            if (transform) setter(endValue); // Can pass null as transform to prevent setting on completion
        }
    }
}