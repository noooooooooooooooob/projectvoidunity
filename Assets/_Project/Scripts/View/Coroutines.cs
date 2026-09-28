using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.View
{
    public static class Coroutines
    {
        /// <summary>duration 동안 0→1 진행률로 step 을 부르고, 마지막에 1 로 한 번 더 부른다.</summary>
        public static IEnumerator Tween(float duration, Action<float> step)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                step(elapsed / duration);
                yield return null;
                elapsed += Time.deltaTime;
            }
            step(1f);
        }

        /// <summary>
        /// 중첩 IEnumerator 를 대기 없이 끝까지 돌린다 (즉시 재생 테스트용). WaitForSeconds 같은 대기 객체는 건너뛴다.
        /// Tween 처럼 시간이 흘러야 끝나는 루틴을 넘기면 멈추지 않으므로 상한을 둔다.
        /// </summary>
        public static void Drain(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            for (int guard = 0; stack.Count > 0; guard++)
            {
                if (guard > 100000)
                {
                    throw new InvalidOperationException("Drain did not finish; a time-based routine was drained.");
                }
                IEnumerator top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
            }
        }
    }
}
