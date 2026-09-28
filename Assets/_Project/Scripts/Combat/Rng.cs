using System;

namespace ProjectVoid.Combat
{
    /// <summary>시드를 고정할 수 있는 난수 생성기 (Godot RandomNumberGenerator 대체). 수열 자체는 Godot 과 다르다.</summary>
    public sealed class Rng
    {
        private readonly Random _random;

        public Rng(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        /// <summary>지금까지 뽑은 횟수. Godot 테스트가 rng.state 로 확인하던 "난수를 썼는가"를 대신한다.</summary>
        public int Draws { get; private set; }

        /// <summary>min 이상 max 이하 (Godot randi_range 처럼 양끝 포함).</summary>
        public int RangeInclusive(int min, int max)
        {
            Draws++;
            return _random.Next(min, max + 1);
        }

        /// <summary>0 이상 1 미만 (Godot randf).</summary>
        public float NextFloat()
        {
            Draws++;
            return (float)_random.NextDouble();
        }

        /// <summary>이 시드와 salt 로만 정해지는 새 생성기. string.GetHashCode 는 실행마다 달라질 수 있어 FNV-1a 로 직접 섞는다.</summary>
        public Rng Derive(string salt)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in salt)
                {
                    hash = (hash ^ c) * 16777619;
                }
                return new Rng((int)(hash ^ ((uint)Seed * 2654435761u)));
            }
        }
    }
}
