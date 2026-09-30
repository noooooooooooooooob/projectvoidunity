using System.Collections;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>유닛 하나의 2.5D 표현: 빌보드 스프라이트, 머리 위 이름·HP 바·수치, 발밑 그림자, 클릭 판정 상자, 연출.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        public const float SpriteHeight = 1.6f;
        public const float OverheadY = 2.0f;
        public const float HpBarWidth = 0.9f;
        public const float HpBarHeight = 0.1f;
        public const float LungeDistance = 0.4f;
        public const float ActionTime = 0.25f;
        public const float FlashTime = 0.24f;
        public const float PopTime = 0.6f;
        public const float FadeTime = 0.4f;
        public const float MoveTime = 0.25f;
        // 발을 축으로 카메라 반대쪽으로 눕히는 각도. 44° 로 내려다볼 때 판이 덜 눌려 보인다.
        public const float BodyTiltDeg = 20f;
        public const float IdleFps = 8f;
        // 그려진 프레임 동작은 코드 모션보다 길어야 읽힌다. 16장 기준 공격 16fps, 피격 20fps.
        public const float AttackFrameTime = 1.0f;
        public const float HitFrameTime = 0.8f;
        public const float HitWhiteTime = 0.06f;
        public const float DamagePopPunch = 1.6f;
        public const float KillPopPunch = 2f;

        private static readonly Color FlashColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color ContactShadowColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color AllyRingColor = new Color(0.3f, 0.55f, 1f, 0.8f);
        private static readonly Color EnemyRingColor = new Color(1f, 0.3f, 0.25f, 0.8f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        // 피해 숫자가 크게 튀었다가 원래 크기로 돌아오는 시간.
        private const float PopPunchTime = 0.15f;
        private const int SparkCount = 12;
        private static readonly Color SparkHot = new Color(1f, 0.95f, 0.8f);
        private static readonly Color SparkWarm = new Color(1f, 0.55f, 0.15f);
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        // 영혼불 한 알이 피어올라 사라지는 시간과 초당 개수. 몸 그림의 64px 중 16px 정도 크기.
        private const float AuraLifetime = 1.6f;
        private const float AuraRate = 2.5f;
        private const float AuraSize = 0.4f;

        private ViewAssets _assets;
        // 발 위치를 축으로 도는 피벗. 연출(튀어오르기·흔들림)은 이 피벗을 움직인다.
        private Transform _body;
        // _body 아래 발 위치의 피벗. 숨쉬기·예비동작 같은 늘이기/기울이기 자세를 맡는다 (_body 회전은 빌보드 몫).
        private Transform _pose;
        private float _idlePhase;
        // 연출 중에는 그 연출이 자세를 잡으므로 숨쉬기를 멈춘다.
        private bool _acting;
        private Texture _still;
        private Texture2D _idleSheet;
        private Texture2D _attackSheet;
        private Texture2D _hitSheet;
        private int _idleFrameOffset;
        private Material _bodyMaterial;
        private Transform _overhead;
        private TextMeshPro _nameLabel;
        private TextMeshPro _statLabel;
        private SpriteRenderer _hpBack;
        private SpriteRenderer _hpFill;
        private SpriteRenderer _shadow;
        private SpriteRenderer _ring;
        private BoxCollider _pickCollider;
        private int _hp;
        private int _maxHp = 1;
        private int _block;
        private float _whiteUntil;
        private BoardProjection _projection;

        public Unit Unit { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public CellTag PickTag { get; private set; }
        public string StatText => _statLabel.text;
        public float HpFillWidth => _hpFill.transform.localScale.x;
        public MeshRenderer BodyRenderer { get; private set; }
        public Material BodyMaterial => _bodyMaterial;
        public Color ShadowColor => _shadow.color;
        public Color RingColor => _ring.color;
        public Transform PoseTransform => _pose;
        public float AttackDuration => _attackSheet != null ? AttackFrameTime : ActionTime;
        public float HitDuration => _hitSheet != null ? HitFrameTime : FlashTime;

        public int FrameCount(Texture2D sheet) => Mathf.Max(1, sheet.width / sheet.height);

        public void Setup(Unit unit, Sprite sprite, ViewAssets assets, BoardProjection projection = BoardProjection.Perspective3D)
        {
            _projection = projection;
            Unit = unit;
            _assets = assets;

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            // 2D 는 정사영 카메라가 정면을 보므로 판을 돌리거나 눕힐 필요가 없다.
            if (projection == BoardProjection.Perspective3D)
            {
                var billboard = _body.gameObject.AddComponent<Billboard>();
                billboard.yAxisOnly = true;
                billboard.tiltDeg = BodyTiltDeg;
            }

            // SpriteRenderer 는 Lit 조명·그림자를 제대로 받지 못해 판에 텍스처를 입힌 사각형으로 그린다.
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sprite";
            _pose = new GameObject("Pose").transform;
            _pose.SetParent(_body, false);
            // 유닛마다 숨쉬는 박자를 어긋나게.
            _idlePhase = unit.UnitId * 1.7f;
            quad.transform.SetParent(_pose, false);
            // 클릭 판정은 유닛 루트의 상자가 맡는다. Destroy 는 프레임 끝이라 먼저 꺼 둔다.
            Collider quadCollider = quad.GetComponent<Collider>();
            quadCollider.enabled = false;
            DestroyImmediateOrLater(quadCollider);
            float aspect = sprite.rect.width / sprite.rect.height;
            // 스프라이트는 오른쪽을 본다. 적은 왼쪽(아군 쪽)을 보도록 좌우를 뒤집는다 (머티리얼이 양면이라 음수 스케일 가능).
            float facing = unit.IsAlly ? 1f : -1f;
            quad.transform.localScale = new Vector3(SpriteHeight * aspect * facing, SpriteHeight, 1f);
            quad.transform.localPosition = new Vector3(0f, SpriteHeight / 2f, 0f);
            _bodyMaterial = new Material(assets.unitMaterial);
            _still = sprite.texture;
            _idleSheet = unit.Data.idleSheet;
            _attackSheet = unit.Data.attackSheet;
            _hitSheet = unit.Data.hitSheet;
            _idleFrameOffset = unit.UnitId * 5;
            ReturnToIdle();
            BodyRenderer = quad.GetComponent<MeshRenderer>();
            BodyRenderer.sharedMaterial = _bodyMaterial;
            BodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            BodyRenderer.receiveShadows = false;
            // 흰 번쩍임은 발광으로 칠한다. 평소엔 검정이라 아무 영향이 없다.
            _bodyMaterial.EnableKeyword("_EMISSION");
            _bodyMaterial.SetColor(EmissionColorId, Color.black);
            Sparks = MakeSparks();
            if (unit.Data.auraSprite != null)
            {
                Aura = MakeAura(unit.Data.auraSprite);
            }

            _shadow = FloorDecal("Shadow", assets.Shadow, new Vector3(0.9f, 0.5f, 1f), 0.01f);
            _shadow.color = ContactShadowColor;
            _ring = FloorDecal("Ring", assets.Ring, new Vector3(0.95f, 0.6f, 1f), 0.012f);
            _ring.color = unit.IsAlly ? AllyRingColor : EnemyRingColor;

            _overhead = new GameObject("Overhead").transform;
            _overhead.SetParent(transform, false);
            _overhead.localPosition = new Vector3(0f, OverheadY, 0f);
            _overhead.gameObject.AddComponent<Billboard>();
            _nameLabel = MakeLabel(unit.Data.displayName, 1.5f, new Vector3(0f, 0.28f, 0f));
            _hpBack = MakeBar(new Color(0.1f, 0.1f, 0.1f, 0.85f), 10);
            _hpFill = MakeBar(new Color(0.35f, 0.85f, 0.4f), 11);
            _statLabel = MakeLabel("", 1.2f, new Vector3(0f, -0.18f, 0f));

            _pickCollider = gameObject.AddComponent<BoxCollider>();
            _pickCollider.size = new Vector3(0.8f, SpriteHeight, 0.4f);
            _pickCollider.center = new Vector3(0f, SpriteHeight / 2f, 0f);
            PickTag = gameObject.AddComponent<CellTag>();

            SetStats(unit.Hp, unit.Data.maxHp, unit.Block);
        }

        private void Update()
        {
            TickIdle(Time.time);
            TickFlash(Time.realtimeSinceStartup);
        }

        public void TickIdle(float time)
        {
            if (_acting)
            {
                return;
            }
            if (_idleSheet != null)
            {
                ShowFrame(_idleSheet, Mathf.FloorToInt(time * IdleFps) + _idleFrameOffset);
                return;
            }
            ApplyPose(UnitMotion.Idle(time, _idlePhase));
        }

        // 띠 텍스처의 한 칸만 보이게 UV 를 옮긴다. 텍스처만 바뀌므로 조명·그림자는 그대로.
        private void ShowFrame(Texture2D sheet, int index)
        {
            int count = FrameCount(sheet);
            _bodyMaterial.SetTexture(BaseMapId, sheet);
            _bodyMaterial.SetTextureScale(BaseMapId, new Vector2(1f / count, 1f));
            _bodyMaterial.SetTextureOffset(BaseMapId, new Vector2((float)(index % count) / count, 0f));
        }

        private void ShowSheetProgress(Texture2D sheet, float t)
            => ShowFrame(sheet, Mathf.Min(Mathf.FloorToInt(t * FrameCount(sheet)), FrameCount(sheet) - 1));

        private void ReturnToIdle()
        {
            if (_idleSheet != null)
            {
                ShowFrame(_idleSheet, _idleFrameOffset);
                return;
            }
            _bodyMaterial.SetTexture(BaseMapId, _still);
            _bodyMaterial.SetTextureScale(BaseMapId, Vector2.one);
            _bodyMaterial.SetTextureOffset(BaseMapId, Vector2.zero);
        }

        public void ApplyPose(UnitMotion.Pose pose)
        {
            // 위로 늘면 옆으로 얇아져 부피가 유지돼 보인다.
            _pose.localScale = new Vector3(1f - pose.stretch * 0.5f, 1f + pose.stretch, 1f);
            // 스프라이트 판이 좌우 반전돼 있어도 "뒤"는 바라보는 방향의 반대여야 한다.
            float facing = Unit.IsAlly ? 1f : -1f;
            _pose.localRotation = Quaternion.Euler(0f, 0f, pose.lean * facing);
        }

        public void SetStats(int hp, int maxHp, int block)
        {
            _hp = hp;
            _maxHp = Mathf.Max(maxHp, 1);
            _block = block;
            RefreshStats();
        }

        public void SetHp(int hp, int maxHp)
        {
            _hp = hp;
            _maxHp = Mathf.Max(maxHp, 1);
            RefreshStats();
        }

        public void SetBlock(int block)
        {
            _block = block;
            RefreshStats();
        }

        public void SetHome(Vector3 worldPosition)
        {
            HomePosition = worldPosition;
            transform.position = worldPosition;
        }

        public IEnumerator SlideTo(Vector3 worldPosition)
        {
            HomePosition = worldPosition;
            Vector3 start = transform.position;
            yield return Coroutines.Tween(MoveTime, t => transform.position = Vector3.Lerp(start, worldPosition, t));
        }

        /// <summary>연출이 중간에 끊겨도 제자리·원래 색으로 돌린다.</summary>
        public void ResetPose()
        {
            transform.position = HomePosition;
            _body.localPosition = Vector3.zero;
            _acting = false;
            _pose.localScale = Vector3.one;
            _pose.localRotation = Quaternion.identity;
            ReturnToIdle();
            _bodyMaterial.SetColor(BaseColorId, Color.white);
        }

        public void SetAlive(bool alive)
        {
            gameObject.SetActive(alive);
        }

        /// <summary>distance 만큼 target 쪽으로 나갔다 돌아온다. 음수면 뒤로 물러나는 반동이다 (원거리).</summary>
        public IEnumerator LungeToward(Vector3 worldTarget, float distance = LungeDistance)
        {
            Vector3 direction = worldTarget - HomePosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0f)
            {
                direction.Normalize();
            }
            Vector3 home = HomePosition;
            Vector3 lunge = home + direction * distance;
            _acting = true;
            yield return Coroutines.Tween(AttackDuration, t =>
            {
                transform.position = Vector3.Lerp(home, lunge, UnitMotion.LungeReach(t));
                // 그려진 프레임 위에 늘이기·기울이기를 더하면 과해진다.
                if (_attackSheet != null)
                {
                    ShowSheetProgress(_attackSheet, t);
                }
                else
                {
                    ApplyPose(UnitMotion.Attack(t));
                }
            });
            _acting = false;
            ReturnToIdle();
        }

        public IEnumerator Hop()
        {
            Transform sprite = _body;
            _acting = true;
            yield return Coroutines.Tween(ActionTime, t =>
            {
                sprite.localPosition = new Vector3(0f, UnitMotion.HopHeight(t), 0f);
                ApplyPose(UnitMotion.Hop(t));
            });
            _acting = false;
        }

        // Godot: 색 번쩍임 2회 + 좌우 흔들림(0.08, -0.08, 0.05, 0)을 FLASH_TIME 동안 동시에.
        /// <summary>knockback 은 맞아서 밀려날 최대 변위(바닥 평면). 끝나면 정확히 제자리로 돌아온다.</summary>
        public IEnumerator FlashAndShake(Vector3 knockback = default)
        {
            bool shoved = knockback != Vector3.zero;
            Vector3 home = HomePosition;
            float[] keys = { 0f, 0.08f, -0.08f, 0.05f, 0f };
            Transform sprite = _body;
            _acting = true;
            StartImpact();
            yield return Coroutines.Tween(HitDuration, t =>
            {
                if (_hitSheet != null)
                {
                    ShowSheetProgress(_hitSheet, t);
                }
                else
                {
                    ApplyPose(UnitMotion.Hit(t));
                }
                int quarter = Mathf.Min((int)(t * 4f), 3);
                _bodyMaterial.SetColor(BaseColorId, quarter % 2 == 0 && t < 1f ? FlashColor : Color.white);
                float local = t * 4f - quarter;
                sprite.localPosition = new Vector3(Mathf.Lerp(keys[quarter], keys[quarter + 1], local), 0f, 0f);
                if (shoved)
                {
                    transform.position = home + knockback * UnitMotion.KnockbackReach(t);
                }
            });
            if (shoved)
            {
                transform.position = home;
            }
            _acting = false;
            ReturnToIdle();
        }

        public ParticleSystem Sparks { get; private set; }
        public ParticleSystem Aura { get; private set; }

        /// <summary>맞는 첫 순간: 몸을 완전한 흰색으로 칠하고 불꽃을 튀긴다. 히트스톱 중에도 끝나도록 실제 시간으로 잰다.</summary>
        public void StartImpact()
        {
            _bodyMaterial.SetColor(EmissionColorId, Color.white);
            _whiteUntil = Time.realtimeSinceStartup + HitWhiteTime;
            Sparks.Emit(SparkCount);
        }

        public void TickFlash(float realtime)
        {
            if (_whiteUntil > 0f && realtime >= _whiteUntil)
            {
                _whiteUntil = 0f;
                _bodyMaterial.SetColor(EmissionColorId, Color.black);
            }
        }

        /// <summary>피해 숫자 크기: punch 배에서 시작해 PopPunchTime 동안 1 로 줄어든다. t 는 PopTime 기준 진행률.</summary>
        public static float PopScale(float t, float punch)
            => Mathf.Lerp(punch, 1f, Mathf.Clamp01(t * PopTime / PopPunchTime));

        public void PopText(string text, Color color, float punch = 1f)
        {
            TextMeshPro label = MakeLabel(text, 2.6f, new Vector3(0f, 0.5f, 0f));
            label.color = color;
            StartCoroutine(PopRoutine(label, punch));
        }

        private IEnumerator PopRoutine(TextMeshPro label, float punch)
        {
            Vector3 start = label.transform.localPosition;
            Color color = label.color;
            yield return Coroutines.Tween(PopTime, t =>
            {
                label.transform.localScale = Vector3.one * PopScale(t, punch);
                label.transform.localPosition = start + new Vector3(0f, 0.6f * t, 0f);
                label.color = new Color(color.r, color.g, color.b, 1f - t);
            });
            Destroy(label.gameObject);
        }

        public IEnumerator FadeOut()
        {
            _pickCollider.enabled = false;
            _hpBack.enabled = false;
            _hpFill.enabled = false;
            _shadow.enabled = false;
            _ring.enabled = false;
            _acting = true;
            if (Aura != null)
            {
                Aura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            yield return Coroutines.Tween(FadeTime, t =>
            {
                ApplyPose(UnitMotion.Death(t));
                float alpha = 1f - t;
                // 알파 잘라내기 머티리얼이라 알파를 내리면 픽셀이 점점 사라진다.
                _bodyMaterial.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
                _nameLabel.alpha = alpha;
                _statLabel.alpha = alpha;
            });
            gameObject.SetActive(false);
        }

        private void RefreshStats()
        {
            _statLabel.text = _block > 0 ? $"{_hp}/{_maxHp}  방{_block}" : $"{_hp}/{_maxHp}";
            float ratio = Mathf.Clamp01((float)_hp / _maxHp);
            Transform fill = _hpFill.transform;
            fill.localScale = new Vector3(HpBarWidth * ratio, HpBarHeight, 1f);
            fill.localPosition = new Vector3(-HpBarWidth * (1f - ratio) / 2f, 0f, 0f);
            _hpFill.enabled = ratio > 0f;
        }

        private SpriteRenderer FloorDecal(string name, Sprite sprite, Vector3 scale, float height)
        {
            var decalObject = new GameObject(name);
            decalObject.transform.SetParent(transform, false);
            if (_projection == BoardProjection.Flat2D)
            {
                // 화면 평면에 눕힌 타원. 몸(z=0)과 타일(z=+FlatTileDepth) 사이에 두고, 높을수록(고리) 몸 쪽으로.
                decalObject.transform.localPosition = new Vector3(0f, 0f, Board3D.FlatTileDepth - height);
                decalObject.transform.localRotation = Quaternion.identity;
                decalObject.transform.localScale = new Vector3(scale.x, scale.y * BoardLayout.FlatRowScale, scale.z);
            }
            else
            {
                decalObject.transform.localPosition = new Vector3(0f, height, 0f);
                decalObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                decalObject.transform.localScale = scale;
            }
            var decal = decalObject.AddComponent<SpriteRenderer>();
            decal.sprite = sprite;
            return decal;
        }

        private static void DestroyImmediateOrLater(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        // 가슴 높이에서 사방으로 튀는 네모 불꽃. 히트스톱(timeScale 0.05) 중에도 날아가도록 실제 시간으로 돈다.
        private ParticleSystem MakeSparks()
        {
            var sparkObject = new GameObject("Sparks");
            sparkObject.transform.SetParent(transform, false);
            sparkObject.transform.localPosition = new Vector3(0f, SpriteHeight * 0.55f, 0f);
            var sparks = sparkObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparks.main;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(SparkHot, SparkWarm);
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            sparkObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = BattleEnvironment.DustMaterial();
            return sparks;
        }

        // 몸 둘레에서 천천히 떠올라 옅어지며 사라지는 불꽃. 몸이 튀거나 밀려도 이미 뜬 불꽃은 제자리에 남는다.
        private ParticleSystem MakeAura(Sprite sprite)
        {
            var auraObject = new GameObject("Aura");
            auraObject.transform.SetParent(transform, false);
            auraObject.transform.localPosition = new Vector3(0f, SpriteHeight * 0.45f, 0f);
            var aura = auraObject.AddComponent<ParticleSystem>();
            aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = aura.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = AuraLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(AuraSize * 0.7f, AuraSize);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = aura.emission;
            emission.rateOverTime = AuraRate;
            ParticleSystem.ShapeModule shape = aura.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.1f, SpriteHeight * 0.7f, 0.1f);
            // 박스 모양 기본값은 앞(+Z)으로 쏜다. 위로 피어오르게 돌린다.
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule fade = aura.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.SizeOverLifetimeModule shrink = aura.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));
            auraObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = AuraMaterial(sprite.texture);
            aura.Play();
            return aura;
        }

        // 픽셀 그림을 그대로 보이게 알파 섞기(더하기 아님)로 그린다.
        private static Material AuraMaterial(Texture texture)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture(BaseMapId, texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        private TextMeshPro MakeLabel(string text, float fontSize, Vector3 localPosition)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(_overhead, false);
            labelObject.transform.localPosition = localPosition;
            var label = labelObject.AddComponent<TextMeshPro>();
            label.font = _assets.font;
            label.fontSharedMaterial = _assets.overlayTextMaterial;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            label.text = text;
            return label;
        }

        private SpriteRenderer MakeBar(Color color, int sortingOrder)
        {
            var barObject = new GameObject("Bar");
            barObject.transform.SetParent(_overhead, false);
            barObject.transform.localScale = new Vector3(HpBarWidth, HpBarHeight, 1f);
            var bar = barObject.AddComponent<SpriteRenderer>();
            bar.sprite = _assets.White;
            bar.color = color;
            bar.sortingOrder = sortingOrder;
            return bar;
        }
    }
}
