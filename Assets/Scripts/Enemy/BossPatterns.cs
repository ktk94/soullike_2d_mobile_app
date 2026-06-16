using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Enemy
{
    /// <summary>
    /// 보스 패턴 예시 모음.
    /// 각 패턴은 BossBase를 인자로 받는 코루틴이며,
    /// 경고(telegraph) 시간을 포함하여 플레이어에게 회피 기회를 준다.
    /// </summary>
    public static class BossPatterns
    {
        // ── 설정 상수 ──────────────────────────────────────
        private const float DEFAULT_TELEGRAPH_TIME = 0.8f;
        private const string PROJECTILE_POOL_KEY = "BossProjectile";
        private const string MINION_POOL_KEY = "Minion";
        private const string WARNING_POOL_KEY = "WarningCircle";

        // =====================================================================
        // 1. ChargeAttack — 돌진 공격
        //    경고 표시 → 짧은 준비 → 빠르게 돌진
        // =====================================================================
        public static IEnumerator ChargeAttack(BossBase boss)
        {
            float telegraphTime = DEFAULT_TELEGRAPH_TIME;
            float chargeSpeed = boss.GetBossSpeed() * 4f;
            float chargeDuration = 0.4f;
            int damage = boss.GetBossAttack();
            LayerMask playerLayer = LayerMask.GetMask("Player");

            Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
            Transform player = FindPlayer();
            if (player == null) yield break;

            // 돌진 방향 결정 (경고 시점의 플레이어 위치)
            Vector2 direction = ((Vector2)player.position - (Vector2)boss.transform.position).normalized;

            // ── 경고 단계 ──
            SpriteRenderer sr = boss.GetComponent<SpriteRenderer>();
            Color originalColor = sr.color;
            sr.color = Color.red;

            // 경고 표시: 돌진 방향으로 라인 이펙트 (풀에서 가져올 수 있음)
            ShowWarningLine(boss.transform.position, direction, chargeSpeed * chargeDuration);

            yield return new WaitForSeconds(telegraphTime);

            sr.color = originalColor;

            // ── 돌진 단계 ──
            float elapsed = 0f;
            while (elapsed < chargeDuration)
            {
                rb.linearVelocity = direction * chargeSpeed;

                // 돌진 중 플레이어와 충돌 체크
                Collider2D hit = Physics2D.OverlapCircle(
                    boss.transform.position, 0.8f, playerLayer);

                if (hit != null)
                {
                    GameEventSystem.Publish(new DamageEvent
                    {
                        Attacker = boss.gameObject,
                        Target = hit.gameObject,
                        Damage = damage,
                        IsCritical = false,
                        Type = DamageType.Physical,
                        HitPoint = boss.transform.position
                    });
                    break; // 한 번 타격 시 돌진 종료
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            rb.linearVelocity = Vector2.zero;

            // 돌진 후 딜레이
            yield return new WaitForSeconds(0.5f);
        }

        // =====================================================================
        // 2. CircularBurst — 원형 투사체 발사
        //    경고 → 전방위로 투사체 발사
        // =====================================================================
        public static IEnumerator CircularBurst(BossBase boss)
        {
            float telegraphTime = DEFAULT_TELEGRAPH_TIME;
            int projectileCount = 12;
            float projectileSpeed = 5f;

            // ── 경고 단계 ──
            SpriteRenderer sr = boss.GetComponent<SpriteRenderer>();
            Color originalColor = sr.color;

            // 점멸 경고
            for (int i = 0; i < 3; i++)
            {
                sr.color = Color.magenta;
                yield return new WaitForSeconds(telegraphTime / 6f);
                sr.color = originalColor;
                yield return new WaitForSeconds(telegraphTime / 6f);
            }

            // ── 발사 단계 ──
            float angleStep = 360f / projectileCount;

            for (int i = 0; i < projectileCount; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // 오브젝트 풀에서 투사체 생성
                if (ObjectPool.Instance != null)
                {
                    GameObject proj = ObjectPool.Instance.Spawn(
                        PROJECTILE_POOL_KEY,
                        boss.transform.position,
                        Quaternion.identity);

                    if (proj != null)
                    {
                        Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
                        if (projRb != null)
                            projRb.linearVelocity = dir * projectileSpeed;

                        // 일정 시간 후 자동 회수
                        ObjectPool.Instance.Despawn(PROJECTILE_POOL_KEY, proj, 4f);
                    }
                }
            }

            yield return new WaitForSeconds(1f);
        }

        // =====================================================================
        // 3. SummonMinions — 잡몹 소환
        //    경고 → 보스 주변에 잡몹 N마리 소환 (최대 제한)
        // =====================================================================
        public static IEnumerator SummonMinions(BossBase boss)
        {
            float telegraphTime = 1.0f;
            int summonCount = 3;
            int maxMinions = 5;
            float summonRadius = 2.5f;

            // 현재 활성 미니언 수 확인
            int existingMinions = GameObject.FindGameObjectsWithTag("Enemy").Length - 1; // 보스 제외
            int actualSummon = Mathf.Min(summonCount, maxMinions - existingMinions);
            if (actualSummon <= 0) yield break;

            // ── 경고 단계 ──
            SpriteRenderer sr = boss.GetComponent<SpriteRenderer>();
            Color originalColor = sr.color;
            sr.color = new Color(0.5f, 0f, 1f); // 보라색 경고

            yield return new WaitForSeconds(telegraphTime);

            sr.color = originalColor;

            // ── 소환 단계 ──
            for (int i = 0; i < actualSummon; i++)
            {
                float angle = (360f / actualSummon) * i * Mathf.Deg2Rad;
                Vector3 spawnPos = boss.transform.position +
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * summonRadius;

                if (ObjectPool.Instance != null)
                {
                    ObjectPool.Instance.Spawn(
                        MINION_POOL_KEY,
                        spawnPos,
                        Quaternion.identity);
                }
            }

            yield return new WaitForSeconds(0.5f);
        }

        // =====================================================================
        // 4. GroundSlam — 바닥 찍기 (원형 AoE)
        //    경고 원 표시 → 내려찍기 → AoE 데미지
        // =====================================================================
        public static IEnumerator GroundSlam(BossBase boss)
        {
            float telegraphTime = 1.0f;
            float aoeRadius = 3f;
            int damage = Mathf.RoundToInt(boss.GetBossAttack() * 1.5f);
            LayerMask playerLayer = LayerMask.GetMask("Player");

            // ── 경고 단계: 바닥에 원형 경고 표시 ──
            GameObject warningObj = null;
            if (ObjectPool.Instance != null)
            {
                warningObj = ObjectPool.Instance.Spawn(
                    WARNING_POOL_KEY,
                    boss.transform.position,
                    Quaternion.identity);

                if (warningObj != null)
                    warningObj.transform.localScale = Vector3.one * (aoeRadius * 2f);
            }

            // 보스 살짝 위로 이동 (점프 연출)
            SpriteRenderer sr = boss.GetComponent<SpriteRenderer>();
            Vector3 originalPos = boss.transform.position;

            float jumpHeight = 1f;
            float elapsed = 0f;
            while (elapsed < telegraphTime)
            {
                float t = elapsed / telegraphTime;
                float yOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                boss.transform.position = originalPos + Vector3.up * yOffset;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ── 내려찍기 ──
            boss.transform.position = originalPos;

            // 경고 오브젝트 회수
            if (warningObj != null && ObjectPool.Instance != null)
                ObjectPool.Instance.Despawn(WARNING_POOL_KEY, warningObj);

            // AoE 데미지 판정
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                boss.transform.position, aoeRadius, playerLayer);

            foreach (var hit in hits)
            {
                GameEventSystem.Publish(new DamageEvent
                {
                    Attacker = boss.gameObject,
                    Target = hit.gameObject,
                    Damage = damage,
                    IsCritical = false,
                    Type = DamageType.Physical,
                    HitPoint = boss.transform.position
                });
            }

            // 착지 후 딜레이
            yield return new WaitForSeconds(0.8f);
        }

        // =====================================================================
        // 5. TeleportStrike — 순간이동 후 근접 공격
        //    사라짐 → 경고 표시 → 플레이어 근처 나타남 → 즉시 공격
        // =====================================================================
        public static IEnumerator TeleportStrike(BossBase boss)
        {
            float vanishTime = 0.6f;
            float telegraphTime = 0.5f;
            float strikeRadius = 1.2f;
            int damage = boss.GetBossAttack();
            LayerMask playerLayer = LayerMask.GetMask("Player");

            SpriteRenderer sr = boss.GetComponent<SpriteRenderer>();
            Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
            Transform player = FindPlayer();
            if (player == null) yield break;

            // ── 사라짐 단계 ──
            boss.SetTemporaryInvincible(true);
            rb.linearVelocity = Vector2.zero;

            // 페이드 아웃
            Color c = sr.color;
            float elapsed = 0f;
            while (elapsed < vanishTime * 0.5f)
            {
                c.a = Mathf.Lerp(1f, 0f, elapsed / (vanishTime * 0.5f));
                sr.color = c;
                elapsed += Time.deltaTime;
                yield return null;
            }
            c.a = 0f;
            sr.color = c;

            yield return new WaitForSeconds(vanishTime * 0.5f);

            // ── 경고 단계: 플레이어 근처에 경고 표시 ──
            // 플레이어 뒤쪽에 나타남
            Vector2 playerPos = player.position;
            Vector2 behindPlayer = playerPos - (Vector2)player.right * 1.5f;

            GameObject warningObj = null;
            if (ObjectPool.Instance != null)
            {
                warningObj = ObjectPool.Instance.Spawn(
                    WARNING_POOL_KEY,
                    behindPlayer,
                    Quaternion.identity);
            }

            yield return new WaitForSeconds(telegraphTime);

            // ── 등장 + 공격 단계 ──
            boss.transform.position = (Vector3)behindPlayer;

            // 경고 오브젝트 회수
            if (warningObj != null && ObjectPool.Instance != null)
                ObjectPool.Instance.Despawn(WARNING_POOL_KEY, warningObj);

            // 페이드 인
            elapsed = 0f;
            while (elapsed < 0.15f)
            {
                c.a = Mathf.Lerp(0f, 1f, elapsed / 0.15f);
                sr.color = c;
                elapsed += Time.deltaTime;
                yield return null;
            }
            c.a = 1f;
            sr.color = c;

            boss.SetTemporaryInvincible(false);

            // 즉시 근접 공격
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                boss.transform.position, strikeRadius, playerLayer);

            foreach (var hit in hits)
            {
                GameEventSystem.Publish(new DamageEvent
                {
                    Attacker = boss.gameObject,
                    Target = hit.gameObject,
                    Damage = damage,
                    IsCritical = true,
                    Type = DamageType.Dark,
                    HitPoint = boss.transform.position
                });
            }

            yield return new WaitForSeconds(0.4f);
        }

        // ── Utility ─────────────────────────────────────────

        /// <summary>
        /// 씬에서 Player 태그를 가진 오브젝트를 찾는다.
        /// </summary>
        private static Transform FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform : null;
        }

        /// <summary>
        /// 돌진 경고 라인을 표시한다 (풀 사용 가능 시).
        /// </summary>
        private static void ShowWarningLine(Vector3 origin, Vector2 direction, float length)
        {
            if (ObjectPool.Instance == null) return;

            GameObject warning = ObjectPool.Instance.Spawn(
                WARNING_POOL_KEY, origin, Quaternion.identity);

            if (warning != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                warning.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                warning.transform.localScale = new Vector3(length, 0.5f, 1f);

                ObjectPool.Instance.Despawn(WARNING_POOL_KEY, warning, 1f);
            }
        }
    }
}
