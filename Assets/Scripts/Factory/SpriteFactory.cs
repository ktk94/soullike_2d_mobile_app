using UnityEngine;
using System.Collections.Generic;

namespace SoulCraft.Factory
{
    /// <summary>
    /// Runtime pixel-art sprite factory. Every sprite is hand-drawn via SetPixel
    /// on Texture2D -- no external assets required.
    /// Call Initialize() once at startup, then use GetSprite(key) anywhere.
    /// Art style: minimal pixel art (16-32px), vivid colours, 1px black outlines.
    /// </summary>
    public static class SpriteFactory
    {
        // ── caches ────────────────────────────────────────────────
        private static Dictionary<string, Sprite> _cache;
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
        private static bool _initialized;

        // ── colour palette ────────────────────────────────────────
        static readonly Color CLR      = new Color(0, 0, 0, 0);
        static readonly Color BLK      = new Color(0, 0, 0, 1);
        static readonly Color WHT      = new Color(1, 1, 1, 1);
        static readonly Color GRAY_L   = C(200, 200, 210);
        static readonly Color GRAY     = C(160, 160, 170);
        static readonly Color GRAY_D   = C(100, 100, 110);
        static readonly Color GRAY_DD  = C(60, 60, 65);
        static readonly Color SKIN     = C(255, 210, 170);
        static readonly Color BROWN    = C(139, 90, 43);
        static readonly Color BROWN_D  = C(100, 60, 30);
        static readonly Color BROWN_L  = C(180, 120, 60);
        static readonly Color RED      = C(220, 50, 50);
        static readonly Color RED_D    = C(170, 30, 30);
        static readonly Color RED_L    = C(255, 120, 120);
        static readonly Color ORANGE   = C(255, 150, 30);
        static readonly Color ORANGE_D = C(220, 100, 20);
        static readonly Color YELLOW   = C(255, 230, 50);
        static readonly Color YELLOW_L = C(255, 255, 150);
        static readonly Color GREEN    = C(80, 200, 80);
        static readonly Color GREEN_D  = C(50, 150, 50);
        static readonly Color GREEN_L  = C(140, 230, 140);
        static readonly Color GREEN_DD = C(30, 100, 30);
        static readonly Color BLUE     = C(60, 100, 220);
        static readonly Color BLUE_L   = C(120, 180, 255);
        static readonly Color BLUE_D   = C(30, 50, 150);
        static readonly Color CYAN     = C(100, 220, 255);
        static readonly Color PURPLE   = C(150, 60, 200);
        static readonly Color PURPLE_D = C(80, 20, 120);
        static readonly Color PURPLE_L = C(200, 140, 255);
        static readonly Color GOLD     = C(255, 210, 60);
        static readonly Color GOLD_D   = C(200, 160, 30);
        static readonly Color WHITE_BLUE = C(210, 230, 255);
        static readonly Color ICE      = C(160, 220, 255);
        static readonly Color ICE_D    = C(100, 170, 220);
        static readonly Color FIRE     = C(255, 100, 30);
        static readonly Color FIRE_D   = C(200, 50, 10);
        static readonly Color LIME     = C(180, 240, 100);

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
        static Color CA(int r, int g, int b, int a) => new Color(r / 255f, g / 255f, b / 255f, a / 255f);

        // ═══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════════

        /// <summary>Batch-create every sprite. Call once at startup.</summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _cache = new Dictionary<string, Sprite>(128);

            // Player (32x32)
            BuildPlayerIdle();
            BuildPlayerWalk1();
            BuildPlayerWalk2();
            BuildPlayerAttack1();
            BuildPlayerAttack2();
            BuildPlayerAttack3();
            BuildPlayerDash();
            BuildPlayerHit();

            // Enemies
            BuildEnemySlime();
            BuildEnemySkeleton();
            BuildEnemyBat();
            BuildEnemyFireSpirit();
            BuildEnemyIceGolem();

            // Bosses
            BuildBossElderGrove();
            BuildBossIgnis();
            BuildBossGlacia();
            BuildBossVoltar();
            BuildBossMalrok();

            // Items (16x16)
            BuildItemSword();
            BuildItemArmor();
            BuildItemPotionHP();
            BuildItemPotionMP();
            BuildItemMatFire();
            BuildItemMatIce();
            BuildItemMatLightning();
            BuildItemMatDark();
            BuildItemMatEarth();
            BuildItemMatWind();
            BuildItemMatHoly();
            BuildItemGold();

            // Effects
            BuildFxSlash1();
            BuildFxSlash2();
            BuildFxSlash3();
            BuildFxFire();
            BuildFxIce();
            BuildFxLightning();
            BuildFxDark();
            BuildFxHoly();
            BuildFxHit();
            BuildFxCritical();
            BuildFxHeal();

            // Tiles (16x16)
            BuildTileFloorStone();
            BuildTileFloorWood();
            BuildTileWall();
            BuildTileDoorOpen();
            BuildTileDoorClosed();

            // UI
            BuildUIHeart();
            BuildUIStar();
            BuildUIArrow();
            BuildUIBtnNormal();
            BuildUIBtnPressed();
            BuildUISlot();
            BuildUIJoystickBG();
            BuildUIJoystickHandle();

            _initialized = true;
        }

        /// <summary>Get a cached sprite by key. Auto-initializes if needed.</summary>
        public static Sprite GetSprite(string key)
        {
            if (!_initialized) Initialize();
            if (_cache.TryGetValue(key, out var s)) return s;
            Debug.LogWarning($"[SpriteFactory] Sprite not found: {key}");
            return null;
        }

        /// <summary>Get the raw texture by key.</summary>
        public static Texture2D GetTexture(string key)
        {
            if (_textureCache.TryGetValue(key, out var cached))
                return cached;
            GetSprite(key);
            _textureCache.TryGetValue(key, out var tex);
            return tex;
        }

        /// <summary>Return full cache (read-only use recommended).</summary>
        public static Dictionary<string, Sprite> GetAll()
        {
            if (!_initialized) Initialize();
            return _cache;
        }

        /// <summary>Clear all cached sprites and textures.</summary>
        public static void ClearCache()
        {
            _cache?.Clear();
            _textureCache.Clear();
            _initialized = false;
        }

        // ═══════════════════════════════════════════════════════════
        //  TEXTURE / SPRITE HELPERS
        // ═══════════════════════════════════════════════════════════

        static Texture2D Tex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = CLR;
            t.SetPixels(px);
            return t;
        }

        static Sprite Reg(string key, Texture2D t)
        {
            t.Apply();
            var s = Sprite.Create(t,
                new Rect(0, 0, t.width, t.height),
                new Vector2(0.5f, 0.5f), 16f);
            s.name = key;
            _cache[key] = s;
            _textureCache[key] = t;
            return s;
        }

        // ═══════════════════════════════════════════════════════════
        //  DRAWING PRIMITIVES
        // ═══════════════════════════════════════════════════════════

        static void SP(Texture2D t, int x, int y, Color c)
        {
            if (x >= 0 && x < t.width && y >= 0 && y < t.height)
                t.SetPixel(x, y, c);
        }

        static void DrawRect(Texture2D t, int x0, int y0, int w, int h, Color fill)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    SP(t, x, y, fill);
        }

        static void DrawRectOutline(Texture2D t, int x0, int y0, int w, int h, Color fill, Color outline)
        {
            DrawRect(t, x0, y0, w, h, fill);
            for (int x = x0; x < x0 + w; x++) { SP(t, x, y0, outline); SP(t, x, y0 + h - 1, outline); }
            for (int y = y0; y < y0 + h; y++) { SP(t, x0, y, outline); SP(t, x0 + w - 1, y, outline); }
        }

        static void DrawCircle(Texture2D t, int cx, int cy, int r, Color fill)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r)
                        SP(t, cx + x, cy + y, fill);
        }

        static void DrawCircleOutline(Texture2D t, int cx, int cy, int r, Color fill, Color outline)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int d = x * x + y * y;
                    if (d <= r * r)
                    {
                        bool edge = (x - 1) * (x - 1) + y * y > r * r ||
                                    (x + 1) * (x + 1) + y * y > r * r ||
                                    x * x + (y - 1) * (y - 1) > r * r ||
                                    x * x + (y + 1) * (y + 1) > r * r;
                        SP(t, cx + x, cy + y, edge ? outline : fill);
                    }
                }
        }

        static void DrawEllipse(Texture2D t, int cx, int cy, int rx, int ry, Color fill)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    float dx = (float)x / Mathf.Max(rx, 1);
                    float dy = (float)y / Mathf.Max(ry, 1);
                    if (dx * dx + dy * dy <= 1f)
                        SP(t, cx + x, cy + y, fill);
                }
        }

        static void DrawEllipseOutline(Texture2D t, int cx, int cy, int rx, int ry, Color fill, Color outline)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    float dx = (float)x / Mathf.Max(rx, 1);
                    float dy = (float)y / Mathf.Max(ry, 1);
                    if (dx * dx + dy * dy <= 1f)
                    {
                        float dxp = (float)(x + 1) / Mathf.Max(rx, 1);
                        float dxm = (float)(x - 1) / Mathf.Max(rx, 1);
                        float dyp = (float)(y + 1) / Mathf.Max(ry, 1);
                        float dym = (float)(y - 1) / Mathf.Max(ry, 1);
                        bool edge = dxp * dxp + dy * dy > 1f || dxm * dxm + dy * dy > 1f ||
                                    dx * dx + dyp * dyp > 1f || dx * dx + dym * dym > 1f;
                        SP(t, cx + x, cy + y, edge ? outline : fill);
                    }
                }
        }

        static void DrawLine(Texture2D t, int x0, int y0, int x1, int y1, Color col)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int safety = 0;
            while (safety++ < 1000)
            {
                SP(t, x0, y0, col);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        static void DrawThickLine(Texture2D t, int x0, int y0, int x1, int y1, Color col, int thick)
        {
            int half = thick / 2;
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                    DrawLine(t, x0 + dx, y0 + dy, x1 + dx, y1 + dy, col);
        }

        static void DrawTriangle(Texture2D t, int x0, int y0, int x1, int y1, int x2, int y2, Color fill)
        {
            int minX = Mathf.Min(x0, Mathf.Min(x1, x2));
            int maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
            int minY = Mathf.Min(y0, Mathf.Min(y1, y2));
            int maxY = Mathf.Max(y0, Mathf.Max(y1, y2));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (PtInTri(x, y, x0, y0, x1, y1, x2, y2))
                        SP(t, x, y, fill);
        }

        static bool PtInTri(int px, int py, int x0, int y0, int x1, int y1, int x2, int y2)
        {
            float d1 = TriSign(px, py, x0, y0, x1, y1);
            float d2 = TriSign(px, py, x1, y1, x2, y2);
            float d3 = TriSign(px, py, x2, y2, x0, y0);
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        static float TriSign(int px, int py, int x1, int y1, int x2, int y2)
        {
            return (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
        }

        static void DrawDiamond(Texture2D t, int cx, int cy, int r, Color fill)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (Mathf.Abs(x) + Mathf.Abs(y) <= r)
                        SP(t, cx + x, cy + y, fill);
        }

        static void DrawArc(Texture2D t, int cx, int cy, int r, float startDeg, float endDeg, Color col, int thick)
        {
            int half = thick / 2;
            for (float a = startDeg; a <= endDeg; a += 0.8f)
            {
                float rad = a * Mathf.Deg2Rad;
                int px = cx + Mathf.RoundToInt(Mathf.Cos(rad) * r);
                int py = cy + Mathf.RoundToInt(Mathf.Sin(rad) * r);
                for (int dy = -half; dy <= half; dy++)
                    for (int dx = -half; dx <= half; dx++)
                        SP(t, px + dx, py + dy, col);
            }
        }

        static void DrawCross(Texture2D t, int cx, int cy, int size, int thick, Color col)
        {
            DrawRect(t, cx - size, cy - thick / 2, size * 2 + 1, thick, col);
            DrawRect(t, cx - thick / 2, cy - size, thick, size * 2 + 1, col);
        }

        /// <summary>Auto-outline: adds 1px black border around all opaque pixels.</summary>
        static void AutoOutline(Texture2D t, Color outlineCol)
        {
            int w = t.width, h = t.height;
            Color[] src = t.GetPixels();
            Color[] dst = (Color[])src.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (src[y * w + x].a > 0.01f) continue;
                    bool adj = false;
                    if (x > 0     && src[y * w + x - 1].a > 0.5f) adj = true;
                    if (x < w - 1 && src[y * w + x + 1].a > 0.5f) adj = true;
                    if (y > 0     && src[(y - 1) * w + x].a > 0.5f) adj = true;
                    if (y < h - 1 && src[(y + 1) * w + x].a > 0.5f) adj = true;
                    if (adj) dst[y * w + x] = outlineCol;
                }
            t.SetPixels(dst);
        }

        // ═══════════════════════════════════════════════════════════
        //  PLAYER SPRITES  (32x32)
        //  Coordinate system: (0,0) = bottom-left
        //  The knight faces forward (toward camera / down).
        // ═══════════════════════════════════════════════════════════

        static void DrawPlayerBodyCore(Texture2D t, int legLOff, int legROff)
        {
            // --- Feet / Boots ---
            DrawRect(t, 10, 1 + legLOff, 5, 2, GRAY_DD);   // left boot
            DrawRect(t, 17, 1 + legROff, 5, 2, GRAY_DD);   // right boot

            // --- Legs ---
            DrawRect(t, 11, 3 + legLOff, 4, 5, GRAY_D);    // left leg
            DrawRect(t, 17, 3 + legROff, 4, 5, GRAY_D);    // right leg

            // --- Belt ---
            DrawRect(t, 10, 8, 12, 2, BROWN);
            DrawRect(t, 14, 8, 4, 2, GOLD_D);               // buckle

            // --- Torso (silver armour) ---
            DrawRect(t, 10, 10, 12, 8, GRAY);
            DrawRect(t, 12, 11, 8, 5, GRAY_L);               // breastplate highlight
            // armour shadow at bottom of breastplate
            DrawRect(t, 12, 10, 8, 1, GRAY_D);

            // --- Shoulder pads ---
            DrawRect(t, 8, 15, 4, 4, GRAY_D);
            DrawRect(t, 20, 15, 4, 4, GRAY_D);
            SP(t, 9, 17, GRAY_L); SP(t, 21, 17, GRAY_L);    // highlight

            // --- Arms ---
            DrawRect(t, 8, 10, 3, 5, GRAY);                  // left arm
            DrawRect(t, 21, 10, 3, 5, GRAY);                 // right arm

            // --- Neck ---
            DrawRect(t, 14, 18, 4, 2, SKIN);

            // --- Helmet / Head ---
            DrawRect(t, 11, 20, 10, 9, GRAY);                // helmet shell
            DrawRect(t, 12, 21, 8, 7, GRAY_L);               // front plate
            // visor slit
            DrawRect(t, 13, 24, 6, 2, BLK);
            SP(t, 14, 24, BLUE_L);                            // left eye glow
            SP(t, 17, 24, BLUE_L);                            // right eye glow
            // helmet crest
            DrawRect(t, 14, 29, 4, 2, RED);
            SP(t, 15, 30, RED_L);                             // crest highlight
        }

        static void BuildPlayerIdle()
        {
            var t = Tex(32, 32);
            DrawPlayerBodyCore(t, 0, 0);
            // Sword in right hand pointing up-right
            DrawLine(t, 24, 12, 29, 19, GRAY_L);
            DrawLine(t, 25, 12, 30, 19, GRAY_L);
            SP(t, 29, 19, WHT); SP(t, 30, 19, WHT);         // blade tip
            DrawRect(t, 23, 11, 3, 1, GOLD_D);               // guard
            SP(t, 24, 10, BROWN);                             // pommel
            AutoOutline(t, BLK);
            Reg("player_idle", t);
        }

        static void BuildPlayerWalk1()
        {
            var t = Tex(32, 32);
            DrawPlayerBodyCore(t, 2, -1);                     // L leg forward, R leg back
            DrawLine(t, 24, 12, 29, 19, GRAY_L);
            DrawLine(t, 25, 12, 30, 19, GRAY_L);
            SP(t, 29, 19, WHT);
            DrawRect(t, 23, 11, 3, 1, GOLD_D);
            AutoOutline(t, BLK);
            Reg("player_walk_1", t);
        }

        static void BuildPlayerWalk2()
        {
            var t = Tex(32, 32);
            DrawPlayerBodyCore(t, -1, 2);                     // R leg forward, L leg back
            DrawLine(t, 24, 12, 29, 19, GRAY_L);
            DrawLine(t, 25, 12, 30, 19, GRAY_L);
            SP(t, 29, 19, WHT);
            DrawRect(t, 23, 11, 3, 1, GOLD_D);
            AutoOutline(t, BLK);
            Reg("player_walk_2", t);
        }

        static void BuildPlayerAttack1()
        {
            // Horizontal slash -- sword extended right
            var t = Tex(32, 32);
            DrawPlayerBodyCore(t, 0, 0);
            DrawRect(t, 24, 14, 8, 2, GRAY_L);               // blade horizontal
            SP(t, 31, 14, WHT); SP(t, 31, 15, WHT);          // tip
            DrawRect(t, 23, 13, 1, 4, GOLD_D);                // guard
            SP(t, 22, 14, BROWN);                              // handle
            AutoOutline(t, BLK);
            Reg("player_attack_1", t);
        }

        static void BuildPlayerAttack2()
        {
            // Upward slash -- sword pointing straight up
            var t = Tex(32, 32);
            DrawPlayerBodyCore(t, 0, 1);
            DrawRect(t, 23, 19, 2, 10, GRAY_L);               // blade vertical up
            SP(t, 23, 29, WHT); SP(t, 24, 29, WHT);          // tip
            DrawRect(t, 22, 18, 4, 1, GOLD_D);                // guard
            SP(t, 23, 17, BROWN);                              // pommel
            AutoOutline(t, BLK);
            Reg("player_attack_2", t);
        }

        static void BuildPlayerAttack3()
        {
            // Downward smash -- sword angled down-right
            var t = Tex(32, 32);
            DrawPlayerBodyCore(t, 1, -1);
            DrawLine(t, 24, 16, 30, 6, GRAY_L);
            DrawLine(t, 25, 16, 31, 6, GRAY_L);
            SP(t, 30, 6, WHT); SP(t, 31, 6, WHT);
            DrawRect(t, 23, 16, 1, 3, GOLD_D);
            SP(t, 22, 17, BROWN);
            AutoOutline(t, BLK);
            Reg("player_attack_3", t);
        }

        static void BuildPlayerDash()
        {
            var t = Tex(32, 32);
            // Legs stretched backward
            DrawRect(t, 6, 2, 4, 5, GRAY_D);
            DrawRect(t, 5, 1, 5, 2, GRAY_DD);
            DrawRect(t, 11, 3, 4, 5, GRAY_D);
            DrawRect(t, 10, 2, 5, 2, GRAY_DD);
            // Torso shifted forward
            DrawRect(t, 12, 8, 12, 9, GRAY);
            DrawRect(t, 14, 10, 8, 5, GRAY_L);
            DrawRect(t, 12, 8, 12, 2, BROWN);
            DrawRect(t, 16, 8, 4, 2, GOLD_D);
            // Shoulders
            DrawRect(t, 10, 14, 4, 3, GRAY_D);
            DrawRect(t, 22, 14, 4, 3, GRAY_D);
            // Helmet / head tilted forward
            DrawRect(t, 16, 18, 10, 9, GRAY);
            DrawRect(t, 17, 19, 8, 7, GRAY_L);
            DrawRect(t, 18, 22, 6, 2, BLK);
            SP(t, 19, 22, BLUE_L); SP(t, 22, 22, BLUE_L);
            // Sword pointing forward
            DrawRect(t, 26, 13, 6, 2, GRAY_L);
            SP(t, 31, 13, WHT); SP(t, 31, 14, WHT);
            // Speed lines
            DrawLine(t, 2, 6, 8, 6, CA(200, 200, 255, 120));
            DrawLine(t, 1, 10, 7, 10, CA(200, 200, 255, 100));
            DrawLine(t, 3, 14, 9, 14, CA(200, 200, 255, 80));
            AutoOutline(t, BLK);
            Reg("player_dash", t);
        }

        static void BuildPlayerHit()
        {
            var t = Tex(32, 32);
            // Body leaning backwards
            DrawRect(t, 9, 1, 4, 6, GRAY_D);
            DrawRect(t, 15, 1, 4, 6, GRAY_D);
            DrawRect(t, 7, 7, 12, 10, GRAY);
            DrawRect(t, 9, 9, 8, 6, GRAY_L);
            DrawRect(t, 7, 7, 12, 2, BROWN);
            // Head thrown back
            DrawRect(t, 6, 19, 10, 9, GRAY);
            DrawRect(t, 7, 20, 8, 7, GRAY_L);
            DrawRect(t, 8, 23, 6, 2, BLK);
            SP(t, 9, 23, RED); SP(t, 12, 23, RED);            // red pain flash
            DrawRect(t, 9, 28, 4, 2, RED);
            // Hit sparks
            SP(t, 20, 16, WHT); SP(t, 22, 18, YELLOW);
            SP(t, 21, 14, WHT); SP(t, 23, 15, YELLOW);
            // Sword dropping away
            DrawLine(t, 3, 8, 1, 3, GRAY_L);
            DrawLine(t, 4, 8, 2, 3, GRAY_L);
            AutoOutline(t, BLK);
            Reg("player_hit", t);
        }

        // ═══════════════════════════════════════════════════════════
        //  ENEMY SPRITES
        // ═══════════════════════════════════════════════════════════

        static void BuildEnemySlime()
        {
            // 24x24 green slime -- dome body
            var t = Tex(24, 24);
            // Shadow on ground
            DrawEllipse(t, 12, 2, 9, 2, C(30, 80, 30));
            // Main body dome
            DrawEllipse(t, 12, 8, 10, 7, GREEN);
            // Lighter belly highlight
            DrawEllipse(t, 12, 10, 7, 4, GREEN_L);
            // Darker underside
            DrawEllipse(t, 12, 4, 9, 3, GREEN_D);
            // Eyes (white sclera + black pupil)
            DrawRect(t, 7, 10, 3, 4, WHT);
            DrawRect(t, 14, 10, 3, 4, WHT);
            DrawRect(t, 8, 11, 2, 2, BLK);
            DrawRect(t, 15, 11, 2, 2, BLK);
            // Mouth
            DrawRect(t, 10, 7, 4, 1, GREEN_DD);
            // Shine highlight
            SP(t, 8, 14, WHT); SP(t, 9, 13, CA(255, 255, 255, 180));
            AutoOutline(t, BLK);
            Reg("enemy_slime", t);
        }

        static void BuildEnemySkeleton()
        {
            // 28x28 skeleton warrior
            var t = Tex(28, 28);
            // Leg bones
            DrawRect(t, 10, 1, 2, 7, WHT);
            DrawRect(t, 16, 1, 2, 7, WHT);
            // Feet
            DrawRect(t, 9, 0, 4, 2, GRAY_L);
            DrawRect(t, 15, 0, 4, 2, GRAY_L);
            // Pelvis
            DrawRect(t, 10, 7, 8, 3, WHT);
            // Spine
            DrawRect(t, 13, 7, 2, 8, WHT);
            // Ribcage
            DrawRect(t, 9, 11, 10, 5, WHT);
            // Rib gaps
            DrawRect(t, 11, 12, 1, 3, GRAY_D);
            DrawRect(t, 13, 12, 2, 3, GRAY_D);
            DrawRect(t, 16, 12, 1, 3, GRAY_D);
            // Shoulders
            DrawRect(t, 7, 15, 3, 2, WHT);
            DrawRect(t, 18, 15, 3, 2, WHT);
            // Arms (bone)
            DrawRect(t, 7, 10, 2, 5, WHT);
            DrawRect(t, 19, 10, 2, 5, WHT);
            // Skull
            DrawCircle(t, 14, 21, 4, WHT);
            // Eye sockets
            DrawRect(t, 12, 21, 2, 2, BLK);
            DrawRect(t, 15, 21, 2, 2, BLK);
            SP(t, 12, 21, RED); SP(t, 15, 21, RED);           // red glow
            // Nose
            SP(t, 14, 20, GRAY_D);
            // Jaw
            DrawRect(t, 12, 18, 4, 2, WHT);
            DrawLine(t, 12, 18, 15, 18, GRAY_D);              // teeth line
            // Sword in right hand
            DrawRect(t, 21, 10, 2, 10, GRAY_L);
            SP(t, 21, 20, WHT); SP(t, 22, 20, WHT);          // tip
            DrawRect(t, 20, 9, 4, 1, BROWN);                  // guard
            AutoOutline(t, BLK);
            Reg("enemy_skeleton", t);
        }

        static void BuildEnemyBat()
        {
            // 28x24 purple bat
            var t = Tex(28, 24);
            // Central body
            DrawEllipse(t, 14, 10, 4, 5, PURPLE);
            DrawEllipse(t, 14, 9, 3, 3, PURPLE_L);
            // Left wing
            DrawTriangle(t, 10, 11, 1, 20, 5, 15, PURPLE);
            DrawTriangle(t, 8, 12, 0, 18, 4, 11, PURPLE_D);
            // Right wing
            DrawTriangle(t, 18, 11, 27, 20, 23, 15, PURPLE);
            DrawTriangle(t, 20, 12, 24, 11, 27, 18, PURPLE_D);
            // Wing membrane veins
            DrawLine(t, 10, 11, 2, 18, PURPLE_D);
            DrawLine(t, 10, 11, 5, 16, PURPLE_D);
            DrawLine(t, 18, 11, 26, 18, PURPLE_D);
            DrawLine(t, 18, 11, 23, 16, PURPLE_D);
            // Ears (triangles)
            DrawTriangle(t, 11, 15, 12, 19, 13, 15, PURPLE_D);
            DrawTriangle(t, 15, 15, 16, 19, 17, 15, PURPLE_D);
            // Eyes
            SP(t, 12, 12, RED); SP(t, 13, 12, RED);
            SP(t, 15, 12, RED); SP(t, 16, 12, RED);
            // Fangs
            SP(t, 13, 7, WHT); SP(t, 15, 7, WHT);
            AutoOutline(t, BLK);
            Reg("enemy_bat", t);
        }

        static void BuildEnemyFireSpirit()
        {
            // 24x28 flickering flame shape
            var t = Tex(24, 28);
            // Flame base
            DrawEllipse(t, 12, 9, 6, 7, ORANGE);
            DrawEllipse(t, 12, 11, 4, 5, YELLOW);
            DrawEllipse(t, 12, 12, 2, 3, WHT);
            // Flame tongues upward
            DrawTriangle(t, 12, 18, 8, 24, 16, 24, FIRE);
            DrawTriangle(t, 9, 16, 6, 22, 12, 20, ORANGE);
            DrawTriangle(t, 15, 16, 12, 20, 18, 22, ORANGE);
            DrawTriangle(t, 12, 22, 10, 27, 14, 27, RED);
            // Wispy tips
            SP(t, 11, 25, FIRE); SP(t, 13, 26, ORANGE);
            SP(t, 12, 27, FIRE_D);
            // Flicker sparks at edges
            SP(t, 5, 11, ORANGE); SP(t, 19, 10, ORANGE);
            SP(t, 4, 8, YELLOW); SP(t, 20, 12, YELLOW);
            // Eyes
            DrawRect(t, 9, 11, 2, 2, BLK);
            DrawRect(t, 13, 11, 2, 2, BLK);
            SP(t, 9, 11, YELLOW); SP(t, 13, 11, YELLOW);
            // Base wisps
            DrawTriangle(t, 8, 2, 6, 1, 10, 3, ORANGE_D);
            DrawTriangle(t, 16, 2, 14, 1, 18, 3, ORANGE_D);
            AutoOutline(t, BLK);
            Reg("enemy_fire_spirit", t);
        }

        static void BuildEnemyIceGolem()
        {
            // 28x28 angular ice golem
            var t = Tex(28, 28);
            // Blocky legs
            DrawRect(t, 8, 0, 4, 8, ICE_D);
            DrawRect(t, 16, 0, 4, 8, ICE_D);
            // Body
            DrawRect(t, 6, 8, 16, 10, ICE);
            // Chest crystal
            DrawDiamond(t, 14, 13, 3, CYAN);
            SP(t, 14, 13, WHT);
            // Shoulder spikes
            DrawTriangle(t, 6, 17, 2, 22, 6, 14, ICE);
            DrawTriangle(t, 22, 17, 22, 14, 26, 22, ICE);
            // Arms
            DrawRect(t, 3, 9, 3, 7, ICE_D);
            DrawRect(t, 22, 9, 3, 7, ICE_D);
            // Fists
            DrawRect(t, 3, 8, 3, 3, ICE);
            DrawRect(t, 22, 8, 3, 3, ICE);
            // Head
            DrawRect(t, 9, 18, 10, 8, ICE);
            DrawRect(t, 10, 19, 8, 6, CYAN);
            // Eyes
            DrawRect(t, 11, 22, 2, 2, BLUE);
            DrawRect(t, 16, 22, 2, 2, BLUE);
            SP(t, 11, 22, WHT); SP(t, 16, 22, WHT);
            // Crown spikes
            DrawTriangle(t, 10, 26, 9, 27, 11, 26, ICE);
            DrawTriangle(t, 14, 26, 13, 27, 15, 26, ICE);
            DrawTriangle(t, 18, 26, 17, 27, 19, 26, ICE);
            // Surface cracks
            DrawLine(t, 8, 15, 12, 10, BLUE_D);
            DrawLine(t, 20, 14, 16, 10, BLUE_D);
            AutoOutline(t, BLK);
            Reg("enemy_ice_golem", t);
        }

        // ═══════════════════════════════════════════════════════════
        //  BOSS SPRITES
        // ═══════════════════════════════════════════════════════════

        static void BuildBossElderGrove()
        {
            // 48x48 giant tree monster
            var t = Tex(48, 48);
            // Roots / feet
            DrawRect(t, 10, 0, 6, 5, BROWN_D);
            DrawRect(t, 32, 0, 6, 5, BROWN_D);
            DrawLine(t, 10, 1, 5, 0, BROWN_D);
            DrawLine(t, 38, 1, 43, 0, BROWN_D);
            // Trunk legs
            DrawRect(t, 11, 5, 6, 10, BROWN);
            DrawRect(t, 31, 5, 6, 10, BROWN);
            // Main trunk body
            DrawRect(t, 13, 14, 22, 18, BROWN);
            DrawRect(t, 15, 16, 18, 14, BROWN_L);
            // Bark texture
            DrawLine(t, 17, 15, 17, 30, BROWN_D);
            DrawLine(t, 21, 14, 21, 28, BROWN_D);
            DrawLine(t, 27, 15, 27, 30, BROWN_D);
            DrawLine(t, 31, 14, 31, 28, BROWN_D);
            // Left branch arm
            DrawRect(t, 5, 20, 8, 4, BROWN);
            DrawRect(t, 1, 22, 5, 3, BROWN);
            DrawTriangle(t, 2, 25, 0, 28, 4, 25, GREEN_D);
            DrawTriangle(t, 5, 24, 3, 28, 7, 26, GREEN_D);
            SP(t, 0, 27, GREEN); SP(t, 1, 28, GREEN);
            // Right branch arm
            DrawRect(t, 35, 20, 8, 4, BROWN);
            DrawRect(t, 42, 22, 5, 3, BROWN);
            DrawTriangle(t, 44, 25, 43, 28, 47, 25, GREEN_D);
            DrawTriangle(t, 41, 24, 39, 28, 43, 26, GREEN_D);
            SP(t, 47, 27, GREEN); SP(t, 46, 28, GREEN);
            // Face carved into trunk
            DrawRect(t, 18, 26, 4, 4, BLK);
            DrawRect(t, 26, 26, 4, 4, BLK);
            SP(t, 19, 27, GREEN); SP(t, 20, 27, GREEN);
            SP(t, 27, 27, GREEN); SP(t, 28, 27, GREEN);
            // Mouth
            DrawRect(t, 20, 20, 8, 3, BLK);
            SP(t, 21, 21, BROWN_D); SP(t, 26, 21, BROWN_D);
            // Leaf canopy
            DrawEllipse(t, 24, 38, 14, 8, GREEN_D);
            DrawEllipse(t, 24, 40, 12, 6, GREEN);
            DrawEllipse(t, 24, 42, 8, 4, GREEN_L);
            // Side leaf clusters
            DrawCircle(t, 13, 36, 4, GREEN_D);
            DrawCircle(t, 35, 36, 4, GREEN_D);
            DrawCircle(t, 13, 38, 3, GREEN);
            DrawCircle(t, 35, 38, 3, GREEN);
            // Moss
            SP(t, 14, 18, GREEN_D); SP(t, 33, 18, GREEN_D);
            AutoOutline(t, BLK);
            Reg("boss_elder_grove", t);
        }

        static void BuildBossIgnis()
        {
            // 56x56 flame demon king
            var t = Tex(56, 56);
            // Legs
            DrawRect(t, 17, 0, 7, 12, RED_D);
            DrawRect(t, 32, 0, 7, 12, RED_D);
            // Clawed feet
            DrawTriangle(t, 15, 0, 18, 4, 13, 0, RED_D);
            DrawTriangle(t, 24, 0, 21, 4, 26, 0, RED_D);
            DrawTriangle(t, 30, 0, 33, 4, 28, 0, RED_D);
            DrawTriangle(t, 41, 0, 38, 4, 43, 0, RED_D);
            // Body
            DrawRect(t, 15, 12, 26, 20, RED);
            DrawRect(t, 19, 16, 18, 14, FIRE);
            // Belly furnace
            DrawEllipse(t, 28, 20, 5, 4, ORANGE);
            DrawEllipse(t, 28, 20, 3, 2, YELLOW);
            SP(t, 28, 20, WHT);
            // Shoulder pauldrons
            DrawCircle(t, 13, 28, 5, RED_D);
            DrawCircle(t, 13, 28, 3, FIRE);
            DrawCircle(t, 43, 28, 5, RED_D);
            DrawCircle(t, 43, 28, 3, FIRE);
            // Arms
            DrawRect(t, 7, 16, 6, 12, RED);
            DrawRect(t, 43, 16, 6, 12, RED);
            // Clawed hands
            DrawTriangle(t, 7, 15, 5, 15, 8, 17, ORANGE);
            DrawTriangle(t, 10, 15, 8, 15, 11, 17, ORANGE);
            DrawTriangle(t, 46, 15, 44, 15, 47, 17, ORANGE);
            DrawTriangle(t, 49, 15, 47, 15, 50, 17, ORANGE);
            // Neck
            DrawRect(t, 24, 32, 8, 4, RED_D);
            // Head
            DrawRect(t, 19, 36, 18, 14, RED);
            DrawRect(t, 21, 38, 14, 10, FIRE);
            // Eyes
            DrawRect(t, 23, 42, 4, 3, YELLOW);
            DrawRect(t, 31, 42, 4, 3, YELLOW);
            SP(t, 24, 43, WHT); SP(t, 32, 43, WHT);
            // Mouth
            DrawRect(t, 25, 37, 6, 3, BLK);
            DrawTriangle(t, 26, 37, 26, 39, 28, 37, ORANGE);
            DrawTriangle(t, 30, 37, 30, 39, 28, 37, ORANGE);
            // Flame crown
            DrawTriangle(t, 21, 50, 19, 55, 23, 50, GOLD);
            DrawTriangle(t, 26, 50, 24, 55, 28, 50, GOLD);
            DrawTriangle(t, 31, 50, 29, 55, 33, 50, GOLD);
            DrawTriangle(t, 36, 50, 34, 55, 38, 50, GOLD);
            SP(t, 19, 55, YELLOW); SP(t, 24, 55, YELLOW);
            SP(t, 29, 55, YELLOW); SP(t, 34, 55, YELLOW);
            // Fire aura
            SP(t, 4, 20, FIRE); SP(t, 52, 22, FIRE);
            SP(t, 3, 24, ORANGE); SP(t, 53, 18, ORANGE);
            DrawTriangle(t, 11, 34, 9, 40, 13, 36, ORANGE);
            DrawTriangle(t, 45, 34, 43, 40, 47, 36, ORANGE);
            AutoOutline(t, BLK);
            Reg("boss_ignis", t);
        }

        static void BuildBossGlacia()
        {
            // 48x56 ice queen
            var t = Tex(48, 56);
            // Flowing ice dress
            DrawTriangle(t, 24, 0, 6, 24, 42, 24, ICE);
            DrawTriangle(t, 24, 2, 10, 22, 38, 22, CYAN);
            // Dress highlights
            DrawLine(t, 16, 12, 12, 4, WHITE_BLUE);
            DrawLine(t, 32, 12, 36, 4, WHITE_BLUE);
            // Dress crystal patterns
            DrawDiamond(t, 18, 14, 2, WHITE_BLUE);
            DrawDiamond(t, 30, 14, 2, WHITE_BLUE);
            DrawDiamond(t, 24, 8, 2, WHT);
            // Torso
            DrawRect(t, 18, 24, 12, 12, ICE);
            DrawRect(t, 20, 26, 8, 8, CYAN);
            DrawDiamond(t, 24, 30, 2, WHT);
            // Arms
            DrawRect(t, 13, 26, 5, 3, ICE);
            DrawRect(t, 30, 26, 5, 3, ICE);
            DrawLine(t, 13, 27, 7, 22, ICE);
            DrawLine(t, 14, 27, 8, 22, ICE);
            DrawLine(t, 35, 27, 41, 22, ICE);
            DrawLine(t, 34, 27, 40, 22, ICE);
            // Ice orbs in hands
            DrawCircle(t, 7, 21, 2, CYAN);
            SP(t, 7, 22, WHT);
            DrawCircle(t, 41, 21, 2, CYAN);
            SP(t, 41, 22, WHT);
            // Neck
            DrawRect(t, 22, 36, 4, 3, WHITE_BLUE);
            // Head
            DrawEllipse(t, 24, 42, 6, 6, WHITE_BLUE);
            DrawEllipse(t, 24, 42, 5, 5, ICE);
            // Eyes
            DrawRect(t, 21, 43, 2, 2, BLUE);
            DrawRect(t, 26, 43, 2, 2, BLUE);
            SP(t, 21, 43, CYAN); SP(t, 26, 43, CYAN);
            // Lips
            DrawRect(t, 23, 40, 3, 1, BLUE_L);
            // Ice crown
            DrawTriangle(t, 18, 47, 17, 52, 20, 48, ICE);
            DrawTriangle(t, 22, 48, 21, 54, 24, 49, CYAN);
            DrawTriangle(t, 26, 48, 24, 54, 28, 49, CYAN);
            DrawTriangle(t, 30, 47, 28, 52, 31, 48, ICE);
            SP(t, 18, 50, BLUE); SP(t, 22, 52, BLUE);
            SP(t, 26, 52, BLUE); SP(t, 30, 50, BLUE);
            // Snowflake particles
            SP(t, 4, 40, WHT); SP(t, 44, 38, WHT);
            SP(t, 8, 46, CYAN); SP(t, 40, 44, CYAN);
            SP(t, 2, 30, WHT); SP(t, 46, 32, WHT);
            SP(t, 10, 50, WHITE_BLUE); SP(t, 38, 48, WHITE_BLUE);
            AutoOutline(t, BLK);
            Reg("boss_glacia", t);
        }

        static void BuildBossVoltar()
        {
            // 52x56 lightning giant
            var t = Tex(52, 56);
            // Legs
            DrawRect(t, 14, 0, 8, 14, PURPLE_D);
            DrawRect(t, 30, 0, 8, 14, PURPLE_D);
            DrawRect(t, 12, 0, 10, 3, PURPLE);
            DrawRect(t, 30, 0, 10, 3, PURPLE);
            // Body
            DrawRect(t, 12, 14, 28, 20, PURPLE);
            DrawRect(t, 16, 18, 20, 14, PURPLE_L);
            // Lightning rune on chest
            DrawLine(t, 26, 26, 22, 22, YELLOW);
            DrawLine(t, 22, 22, 28, 18, YELLOW);
            DrawLine(t, 28, 18, 24, 16, YELLOW);
            SP(t, 25, 22, WHT); SP(t, 23, 20, WHT);
            // Spiked shoulder pads
            DrawTriangle(t, 10, 30, 4, 36, 12, 32, PURPLE_D);
            DrawTriangle(t, 42, 30, 40, 32, 48, 36, PURPLE_D);
            SP(t, 4, 36, YELLOW); SP(t, 48, 36, YELLOW);
            // Arms
            DrawRect(t, 6, 18, 6, 12, PURPLE);
            DrawRect(t, 40, 18, 6, 12, PURPLE);
            // Lightning on arms
            DrawLine(t, 7, 20, 9, 24, YELLOW);
            DrawLine(t, 9, 24, 7, 28, YELLOW);
            DrawLine(t, 43, 20, 45, 24, YELLOW);
            DrawLine(t, 45, 24, 43, 28, YELLOW);
            // Fists
            DrawRect(t, 5, 16, 7, 4, PURPLE_D);
            DrawRect(t, 40, 16, 7, 4, PURPLE_D);
            // Neck
            DrawRect(t, 22, 34, 8, 4, PURPLE);
            // Head
            DrawRect(t, 18, 38, 16, 12, PURPLE);
            DrawRect(t, 20, 40, 12, 8, PURPLE_L);
            // Eyes
            DrawRect(t, 22, 44, 3, 3, YELLOW);
            DrawRect(t, 29, 44, 3, 3, YELLOW);
            SP(t, 23, 45, WHT); SP(t, 30, 45, WHT);
            // Mouth
            DrawRect(t, 24, 40, 6, 2, BLK);
            // Lightning horns
            DrawLine(t, 18, 50, 14, 55, YELLOW);
            DrawLine(t, 14, 55, 16, 52, YELLOW);
            DrawLine(t, 16, 52, 13, 56, YELLOW);
            DrawLine(t, 34, 50, 38, 55, YELLOW);
            DrawLine(t, 38, 55, 36, 52, YELLOW);
            DrawLine(t, 36, 52, 39, 56, YELLOW);
            // Center bolt
            DrawLine(t, 26, 50, 24, 54, YELLOW);
            DrawLine(t, 24, 54, 28, 52, YELLOW);
            DrawLine(t, 28, 52, 26, 55, YELLOW);
            SP(t, 26, 55, WHT);
            // Sparks
            SP(t, 2, 24, YELLOW); SP(t, 50, 26, YELLOW);
            SP(t, 4, 14, YELLOW_L); SP(t, 48, 16, YELLOW_L);
            AutoOutline(t, BLK);
            Reg("boss_voltar", t);
        }

        static void BuildBossMalrok()
        {
            // 64x64 lord of the abyss
            var t = Tex(64, 64);
            // Shadow tendrils from feet
            DrawTriangle(t, 18, 0, 10, 0, 16, 4, BLK);
            DrawTriangle(t, 46, 0, 48, 4, 54, 0, BLK);
            DrawLine(t, 12, 1, 8, 0, PURPLE_D);
            DrawLine(t, 52, 1, 56, 0, PURPLE_D);
            // Legs
            DrawRect(t, 20, 0, 7, 12, PURPLE_D);
            DrawRect(t, 37, 0, 7, 12, PURPLE_D);
            // Main body
            DrawRect(t, 16, 12, 32, 26, PURPLE_D);
            DrawRect(t, 20, 16, 24, 20, BLK);
            // Dark armour plates
            DrawRect(t, 18, 24, 28, 10, GRAY_DD);
            DrawRect(t, 20, 26, 24, 6, PURPLE_D);
            // Chest void symbol
            DrawCircle(t, 32, 26, 5, BLK);
            DrawCircle(t, 32, 26, 3, PURPLE);
            SP(t, 32, 26, PURPLE_L);
            // Shoulder spikes
            DrawTriangle(t, 14, 34, 6, 42, 16, 36, GRAY_DD);
            DrawTriangle(t, 50, 34, 48, 36, 58, 42, GRAY_DD);
            DrawTriangle(t, 10, 36, 4, 44, 12, 38, PURPLE_D);
            DrawTriangle(t, 54, 36, 52, 38, 60, 44, PURPLE_D);
            SP(t, 6, 42, PURPLE_L); SP(t, 4, 44, PURPLE_L);
            SP(t, 58, 42, PURPLE_L); SP(t, 60, 44, PURPLE_L);
            // Arms
            DrawRect(t, 6, 18, 10, 14, PURPLE_D);
            DrawRect(t, 48, 18, 10, 14, PURPLE_D);
            // Clawed hands
            for (int i = 0; i < 3; i++)
            {
                DrawTriangle(t, 6 + i * 3, 16, 5 + i * 3, 16, 7 + i * 3, 18, GRAY_DD);
                DrawTriangle(t, 50 + i * 3, 16, 49 + i * 3, 16, 51 + i * 3, 18, GRAY_DD);
            }
            // Neck
            DrawRect(t, 26, 38, 12, 4, PURPLE_D);
            // Head
            DrawRect(t, 22, 42, 20, 16, PURPLE_D);
            DrawRect(t, 24, 44, 16, 12, BLK);
            // THE GLOWING EYES (key feature)
            DrawRect(t, 27, 50, 4, 4, PURPLE_L);
            DrawRect(t, 37, 50, 4, 4, PURPLE_L);
            DrawRect(t, 28, 51, 2, 2, WHT);
            DrawRect(t, 38, 51, 2, 2, WHT);
            // Eye glow
            SP(t, 26, 51, PURPLE); SP(t, 31, 51, PURPLE);
            SP(t, 36, 51, PURPLE); SP(t, 41, 51, PURPLE);
            SP(t, 27, 54, PURPLE); SP(t, 38, 54, PURPLE);
            // Mouth / maw
            DrawRect(t, 29, 44, 6, 4, BLK);
            SP(t, 30, 48, GRAY_L); SP(t, 31, 47, GRAY_L);
            SP(t, 33, 48, GRAY_L); SP(t, 34, 47, GRAY_L);
            // Horns
            DrawTriangle(t, 22, 58, 18, 63, 24, 58, GRAY_DD);
            DrawTriangle(t, 42, 58, 40, 58, 46, 63, GRAY_DD);
            DrawTriangle(t, 32, 58, 29, 63, 35, 63, GRAY_DD);
            SP(t, 32, 62, PURPLE_L);
            // Dark aura
            SP(t, 0, 32, PURPLE_D); SP(t, 63, 30, PURPLE_D);
            SP(t, 2, 40, PURPLE); SP(t, 62, 38, PURPLE);
            SP(t, 6, 48, PURPLE_D); SP(t, 58, 50, PURPLE_D);
            SP(t, 10, 56, PURPLE); SP(t, 54, 54, PURPLE);
            DrawLine(t, 0, 10, 6, 14, CA(80, 20, 120, 150));
            DrawLine(t, 63, 12, 57, 16, CA(80, 20, 120, 150));
            AutoOutline(t, BLK);
            Reg("boss_malrok", t);
        }

        // ═══════════════════════════════════════════════════════════
        //  ITEM SPRITES  (16x16)
        // ═══════════════════════════════════════════════════════════

        static void BuildItemSword()
        {
            var t = Tex(16, 16);
            // Blade (diagonal bottom-left to top-right)
            DrawLine(t, 6, 6, 13, 13, GRAY_L);
            DrawLine(t, 7, 6, 14, 13, GRAY_L);
            DrawLine(t, 6, 7, 13, 14, GRAY);
            // Tip highlight
            SP(t, 13, 13, WHT); SP(t, 14, 13, WHT); SP(t, 13, 14, WHT);
            // Guard
            DrawRect(t, 4, 5, 5, 1, GOLD_D);
            DrawRect(t, 4, 6, 5, 1, GOLD);
            // Handle
            DrawLine(t, 5, 4, 2, 1, BROWN);
            DrawLine(t, 4, 4, 1, 1, BROWN);
            // Pommel
            SP(t, 1, 1, GOLD); SP(t, 2, 1, GOLD); SP(t, 1, 0, GOLD_D);
            AutoOutline(t, BLK);
            Reg("item_sword", t);
        }

        static void BuildItemArmor()
        {
            var t = Tex(16, 16);
            // Chestplate
            DrawRect(t, 3, 2, 10, 10, GRAY);
            DrawRect(t, 5, 4, 6, 6, GRAY_L);
            // Neckline
            DrawRect(t, 5, 11, 6, 2, GRAY_D);
            DrawRect(t, 6, 12, 4, 1, GRAY);
            // Shoulder straps
            DrawRect(t, 2, 10, 3, 3, GRAY_D);
            DrawRect(t, 11, 10, 3, 3, GRAY_D);
            // Belt
            DrawRect(t, 3, 2, 10, 2, BROWN);
            DrawRect(t, 7, 2, 2, 2, GOLD_D);
            // Shine
            SP(t, 8, 8, WHT);
            AutoOutline(t, BLK);
            Reg("item_armor", t);
        }

        static void BuildItemPotionHP()
        {
            var t = Tex(16, 16);
            // Bottle
            DrawRect(t, 4, 1, 8, 8, RED);
            DrawRect(t, 5, 2, 6, 6, C(240, 60, 60));
            // Neck
            DrawRect(t, 6, 9, 4, 3, RED_D);
            // Cork
            DrawRect(t, 6, 12, 4, 2, BROWN);
            DrawRect(t, 7, 14, 2, 1, BROWN_L);
            // Shine
            SP(t, 6, 6, C(255, 140, 140));
            SP(t, 6, 5, C(255, 180, 180));
            // Bottom
            DrawRect(t, 5, 1, 6, 1, RED_D);
            // HP cross label
            DrawRect(t, 7, 4, 2, 4, WHT);
            DrawRect(t, 6, 5, 4, 2, WHT);
            AutoOutline(t, BLK);
            Reg("item_potion_hp", t);
        }

        static void BuildItemPotionMP()
        {
            var t = Tex(16, 16);
            DrawRect(t, 4, 1, 8, 8, BLUE);
            DrawRect(t, 5, 2, 6, 6, BLUE_L);
            DrawRect(t, 6, 9, 4, 3, BLUE_D);
            DrawRect(t, 6, 12, 4, 2, BROWN);
            DrawRect(t, 7, 14, 2, 1, BROWN_L);
            SP(t, 6, 6, C(160, 200, 255));
            SP(t, 6, 5, C(200, 220, 255));
            DrawRect(t, 5, 1, 6, 1, BLUE_D);
            // Star label
            SP(t, 8, 6, WHT); SP(t, 7, 5, WHT); SP(t, 9, 5, WHT);
            SP(t, 7, 4, WHT); SP(t, 9, 4, WHT); SP(t, 8, 3, WHT);
            AutoOutline(t, BLK);
            Reg("item_potion_mp", t);
        }

        static void DrawGem(Texture2D t, Color main, Color light, Color dark)
        {
            DrawDiamond(t, 8, 8, 5, main);
            DrawTriangle(t, 8, 13, 4, 8, 12, 8, light);
            DrawTriangle(t, 8, 3, 4, 8, 12, 8, dark);
            SP(t, 6, 10, WHT);
            SP(t, 7, 11, light);
        }

        static void BuildItemMatFire()
        {
            var t = Tex(16, 16);
            DrawGem(t, ORANGE, YELLOW, FIRE_D);
            AutoOutline(t, BLK);
            Reg("item_mat_fire", t);
        }

        static void BuildItemMatIce()
        {
            var t = Tex(16, 16);
            DrawGem(t, CYAN, WHITE_BLUE, BLUE_D);
            AutoOutline(t, BLK);
            Reg("item_mat_ice", t);
        }

        static void BuildItemMatLightning()
        {
            var t = Tex(16, 16);
            DrawGem(t, YELLOW, YELLOW_L, GOLD_D);
            SP(t, 8, 10, WHT); SP(t, 9, 9, WHT);
            AutoOutline(t, BLK);
            Reg("item_mat_lightning", t);
        }

        static void BuildItemMatDark()
        {
            var t = Tex(16, 16);
            DrawGem(t, PURPLE, PURPLE_L, PURPLE_D);
            AutoOutline(t, BLK);
            Reg("item_mat_dark", t);
        }

        static void BuildItemMatEarth()
        {
            var t = Tex(16, 16);
            DrawGem(t, BROWN, BROWN_L, BROWN_D);
            AutoOutline(t, BLK);
            Reg("item_mat_earth", t);
        }

        static void BuildItemMatWind()
        {
            var t = Tex(16, 16);
            DrawGem(t, LIME, GREEN_L, GREEN_D);
            AutoOutline(t, BLK);
            Reg("item_mat_wind", t);
        }

        static void BuildItemMatHoly()
        {
            var t = Tex(16, 16);
            DrawGem(t, WHT, YELLOW_L, GOLD);
            SP(t, 5, 12, YELLOW_L); SP(t, 11, 12, YELLOW_L);
            SP(t, 8, 14, GOLD);
            AutoOutline(t, BLK);
            Reg("item_mat_holy", t);
        }

        static void BuildItemGold()
        {
            var t = Tex(16, 16);
            // Coin (circle)
            DrawCircleOutline(t, 8, 8, 6, GOLD, GOLD_D);
            DrawCircle(t, 8, 8, 4, GOLD);
            // G / $ symbol
            DrawRect(t, 7, 5, 2, 6, GOLD_D);
            SP(t, 6, 10, GOLD_D); SP(t, 9, 6, GOLD_D);
            DrawRect(t, 6, 9, 4, 1, GOLD_D);
            DrawRect(t, 6, 7, 4, 1, GOLD_D);
            // Shine
            SP(t, 5, 11, YELLOW_L); SP(t, 6, 12, WHT);
            AutoOutline(t, BLK);
            Reg("item_gold", t);
        }

        // ═══════════════════════════════════════════════════════════
        //  EFFECT SPRITES
        // ═══════════════════════════════════════════════════════════

        static void BuildFxSlash1()
        {
            var t = Tex(32, 32);
            // Horizontal arc
            DrawArc(t, 16, 16, 12, 20, 160, WHT, 2);
            DrawArc(t, 16, 16, 10, 30, 150, CA(255, 255, 255, 180), 1);
            DrawArc(t, 16, 16, 14, 15, 165, CA(200, 220, 255, 120), 1);
            Reg("fx_slash_1", t);
        }

        static void BuildFxSlash2()
        {
            var t = Tex(32, 32);
            // Upward arc
            DrawArc(t, 16, 16, 12, 60, 170, WHT, 2);
            DrawArc(t, 16, 16, 10, 70, 160, CA(255, 255, 255, 180), 1);
            DrawArc(t, 16, 16, 14, 55, 175, CA(200, 220, 255, 120), 1);
            Reg("fx_slash_2", t);
        }

        static void BuildFxSlash3()
        {
            var t = Tex(32, 32);
            // Heavy downward arc
            DrawArc(t, 16, 16, 13, 200, 340, WHT, 3);
            DrawArc(t, 16, 16, 11, 210, 330, CA(255, 255, 255, 180), 2);
            DrawArc(t, 16, 16, 15, 195, 345, CA(200, 220, 255, 100), 1);
            Reg("fx_slash_3", t);
        }

        static void BuildFxFire()
        {
            var t = Tex(16, 16);
            DrawEllipse(t, 8, 6, 4, 5, ORANGE);
            DrawEllipse(t, 8, 7, 3, 3, YELLOW);
            SP(t, 8, 8, WHT);
            DrawTriangle(t, 8, 11, 6, 14, 10, 14, RED);
            SP(t, 8, 14, ORANGE);
            SP(t, 5, 4, ORANGE); SP(t, 11, 5, ORANGE);
            Reg("fx_fire", t);
        }

        static void BuildFxIce()
        {
            var t = Tex(16, 16);
            // Main shard
            DrawTriangle(t, 8, 14, 3, 2, 13, 2, ICE);
            DrawTriangle(t, 8, 12, 5, 4, 11, 4, CYAN);
            DrawTriangle(t, 8, 10, 6, 5, 10, 5, WHITE_BLUE);
            SP(t, 8, 8, WHT);
            // Smaller side shards
            DrawTriangle(t, 3, 8, 1, 4, 5, 6, ICE);
            DrawTriangle(t, 13, 9, 11, 5, 15, 7, ICE);
            Reg("fx_ice", t);
        }

        static void BuildFxLightning()
        {
            var t = Tex(16, 16);
            // Zigzag bolt
            DrawLine(t, 8, 15, 6, 11, YELLOW);
            DrawLine(t, 6, 11, 10, 9, YELLOW);
            DrawLine(t, 10, 9, 5, 5, YELLOW);
            DrawLine(t, 5, 5, 9, 3, YELLOW);
            DrawLine(t, 9, 3, 7, 0, YELLOW);
            // Glow layer
            DrawLine(t, 7, 14, 5, 10, YELLOW_L);
            DrawLine(t, 5, 10, 9, 8, YELLOW_L);
            DrawLine(t, 9, 8, 4, 4, YELLOW_L);
            // Sparks
            SP(t, 3, 12, YELLOW); SP(t, 12, 7, YELLOW);
            SP(t, 11, 12, WHT); SP(t, 2, 6, WHT);
            SP(t, 13, 4, YELLOW_L);
            Reg("fx_lightning", t);
        }

        static void BuildFxDark()
        {
            var t = Tex(16, 16);
            // Expanding ring
            DrawCircleOutline(t, 8, 8, 6, CLR, PURPLE);
            DrawCircleOutline(t, 8, 8, 5, CLR, PURPLE_D);
            DrawCircleOutline(t, 8, 8, 4, CLR, CA(150, 60, 200, 150));
            DrawCircle(t, 8, 8, 2, PURPLE_D);
            SP(t, 8, 8, PURPLE_L);
            // Wisps
            SP(t, 3, 3, PURPLE_D); SP(t, 13, 13, PURPLE_D);
            SP(t, 3, 13, PURPLE); SP(t, 13, 3, PURPLE);
            Reg("fx_dark", t);
        }

        static void BuildFxHoly()
        {
            var t = Tex(16, 16);
            // Radiant circles
            DrawCircleOutline(t, 8, 8, 6, CLR, GOLD);
            DrawCircleOutline(t, 8, 8, 5, CLR, YELLOW_L);
            DrawCircleOutline(t, 8, 8, 4, CLR, CA(255, 255, 200, 180));
            DrawCircle(t, 8, 8, 2, YELLOW_L);
            SP(t, 8, 8, WHT);
            // Rays
            DrawLine(t, 8, 14, 8, 2, CA(255, 255, 200, 100));
            DrawLine(t, 2, 8, 14, 8, CA(255, 255, 200, 100));
            SP(t, 4, 4, GOLD); SP(t, 12, 12, GOLD);
            SP(t, 4, 12, GOLD); SP(t, 12, 4, GOLD);
            Reg("fx_holy", t);
        }

        static void BuildFxHit()
        {
            var t = Tex(16, 16);
            // 4-point star burst
            DrawRect(t, 2, 7, 12, 2, WHT);
            DrawRect(t, 7, 2, 2, 12, WHT);
            DrawLine(t, 4, 4, 12, 12, WHT);
            DrawLine(t, 12, 4, 4, 12, WHT);
            DrawRect(t, 6, 6, 4, 4, WHT);
            // Sparkle dots
            SP(t, 1, 8, YELLOW_L); SP(t, 14, 7, YELLOW_L);
            SP(t, 8, 1, YELLOW_L); SP(t, 7, 14, YELLOW_L);
            SP(t, 3, 3, YELLOW_L); SP(t, 12, 3, YELLOW_L);
            SP(t, 3, 12, YELLOW_L); SP(t, 12, 12, YELLOW_L);
            Reg("fx_hit", t);
        }

        static void BuildFxCritical()
        {
            var t = Tex(24, 24);
            // Large star burst + red
            DrawRect(t, 2, 11, 20, 2, WHT);
            DrawRect(t, 11, 2, 2, 20, WHT);
            DrawThickLine(t, 4, 4, 20, 20, WHT, 2);
            DrawThickLine(t, 20, 4, 4, 20, WHT, 2);
            DrawCircle(t, 12, 12, 3, WHT);
            DrawCircle(t, 12, 12, 2, YELLOW);
            SP(t, 12, 12, WHT);
            // Red outer ring
            DrawCircleOutline(t, 12, 12, 10, CLR, RED);
            DrawCircleOutline(t, 12, 12, 9, CLR, CA(255, 100, 100, 150));
            SP(t, 1, 12, RED); SP(t, 23, 12, RED);
            SP(t, 12, 1, RED); SP(t, 12, 23, RED);
            SP(t, 4, 4, ORANGE); SP(t, 20, 20, ORANGE);
            Reg("fx_critical", t);
        }

        static void BuildFxHeal()
        {
            var t = Tex(16, 16);
            // Green cross
            DrawRect(t, 5, 3, 6, 10, GREEN);
            DrawRect(t, 3, 5, 10, 6, GREEN);
            DrawRect(t, 6, 5, 4, 6, GREEN_L);
            DrawRect(t, 5, 6, 6, 4, GREEN_L);
            DrawRect(t, 7, 7, 2, 2, WHT);
            // Sparkles
            SP(t, 2, 2, GREEN_L); SP(t, 13, 13, GREEN_L);
            SP(t, 2, 13, GREEN_L); SP(t, 13, 2, GREEN_L);
            SP(t, 1, 8, CA(140, 230, 140, 150));
            SP(t, 14, 8, CA(140, 230, 140, 150));
            SP(t, 8, 1, CA(140, 230, 140, 150));
            SP(t, 8, 14, CA(140, 230, 140, 150));
            AutoOutline(t, BLK);
            Reg("fx_heal", t);
        }

        // ═══════════════════════════════════════════════════════════
        //  TILE SPRITES  (16x16)
        // ═══════════════════════════════════════════════════════════

        static void BuildTileFloorStone()
        {
            var t = Tex(16, 16);
            DrawRect(t, 0, 0, 16, 16, C(130, 130, 135));
            // Grout lines
            DrawLine(t, 0, 8, 15, 8, C(100, 100, 105));
            DrawLine(t, 8, 0, 8, 7, C(100, 100, 105));
            DrawLine(t, 4, 8, 4, 15, C(100, 100, 105));
            DrawLine(t, 12, 8, 12, 15, C(100, 100, 105));
            // Random texture specks
            SP(t, 3, 3, C(142, 142, 148)); SP(t, 11, 5, C(120, 120, 125));
            SP(t, 6, 12, C(140, 142, 140)); SP(t, 14, 10, C(118, 118, 122));
            SP(t, 2, 14, C(142, 140, 138)); SP(t, 10, 2, C(138, 138, 140));
            SP(t, 1, 6, C(125, 125, 130)); SP(t, 13, 14, C(145, 143, 140));
            Reg("tile_floor_stone", t);
        }

        static void BuildTileFloorWood()
        {
            var t = Tex(16, 16);
            DrawRect(t, 0, 0, 16, 16, BROWN_L);
            // Plank lines
            DrawLine(t, 0, 4, 15, 4, BROWN_D);
            DrawLine(t, 0, 8, 15, 8, BROWN_D);
            DrawLine(t, 0, 12, 15, 12, BROWN_D);
            // Grain
            DrawLine(t, 3, 0, 3, 3, BROWN);
            DrawLine(t, 10, 0, 10, 3, BROWN);
            DrawLine(t, 6, 5, 6, 7, BROWN);
            DrawLine(t, 13, 5, 13, 7, BROWN);
            DrawLine(t, 2, 9, 2, 11, BROWN);
            DrawLine(t, 9, 9, 9, 11, BROWN);
            DrawLine(t, 5, 13, 5, 15, BROWN);
            DrawLine(t, 12, 13, 12, 15, BROWN);
            // Knots
            SP(t, 7, 2, BROWN_D); SP(t, 4, 10, BROWN_D);
            SP(t, 1, 1, C(195, 130, 70)); SP(t, 8, 6, C(195, 130, 70));
            Reg("tile_floor_wood", t);
        }

        static void BuildTileWall()
        {
            var t = Tex(16, 16);
            DrawRect(t, 0, 0, 16, 16, GRAY_D);
            // Brick rows
            DrawRectOutline(t, 0, 0, 7, 4, C(90, 88, 95), C(70, 70, 75));
            DrawRectOutline(t, 8, 0, 8, 4, C(85, 85, 90), C(70, 70, 75));
            DrawRectOutline(t, -4, 4, 8, 4, C(95, 92, 98), C(70, 70, 75));
            DrawRectOutline(t, 4, 4, 8, 4, C(88, 88, 92), C(70, 70, 75));
            DrawRectOutline(t, 12, 4, 8, 4, C(92, 90, 95), C(70, 70, 75));
            DrawRectOutline(t, 0, 8, 7, 4, C(93, 90, 96), C(70, 70, 75));
            DrawRectOutline(t, 8, 8, 8, 4, C(87, 87, 90), C(70, 70, 75));
            DrawRectOutline(t, -4, 12, 8, 4, C(90, 90, 94), C(70, 70, 75));
            DrawRectOutline(t, 4, 12, 8, 4, C(86, 86, 90), C(70, 70, 75));
            DrawRectOutline(t, 12, 12, 8, 4, C(91, 89, 93), C(70, 70, 75));
            Reg("tile_wall", t);
        }

        static void BuildTileDoorOpen()
        {
            var t = Tex(16, 16);
            // Frame
            DrawRect(t, 0, 0, 3, 16, BROWN);
            DrawRect(t, 13, 0, 3, 16, BROWN);
            DrawRect(t, 0, 14, 16, 2, BROWN);
            DrawRect(t, 1, 1, 1, 14, BROWN_L);
            DrawRect(t, 14, 1, 1, 14, BROWN_L);
            // Opening
            DrawRect(t, 3, 0, 10, 14, C(30, 30, 35));
            DrawRect(t, 3, 0, 10, 2, C(120, 120, 125));
            SP(t, 1, 8, BROWN_D); SP(t, 14, 8, BROWN_D);
            Reg("tile_door_open", t);
        }

        static void BuildTileDoorClosed()
        {
            var t = Tex(16, 16);
            // Frame
            DrawRect(t, 0, 0, 2, 16, BROWN_D);
            DrawRect(t, 14, 0, 2, 16, BROWN_D);
            DrawRect(t, 0, 14, 16, 2, BROWN_D);
            // Door panels
            DrawRect(t, 2, 0, 12, 14, BROWN);
            DrawRectOutline(t, 3, 1, 10, 5, BROWN_L, BROWN_D);
            DrawRectOutline(t, 3, 7, 10, 6, BROWN_L, BROWN_D);
            // Lock
            DrawRect(t, 10, 6, 2, 3, GOLD_D);
            SP(t, 11, 7, GOLD);
            SP(t, 10, 7, BLK);
            // Wood grain
            DrawLine(t, 5, 1, 5, 5, BROWN_D);
            DrawLine(t, 5, 7, 5, 12, BROWN_D);
            DrawLine(t, 9, 2, 9, 5, BROWN_D);
            DrawLine(t, 9, 8, 9, 12, BROWN_D);
            Reg("tile_door_closed", t);
        }

        // ═══════════════════════════════════════════════════════════
        //  UI SPRITES
        // ═══════════════════════════════════════════════════════════

        static void BuildUIHeart()
        {
            var t = Tex(16, 16);
            // Two overlapping circles + triangle
            DrawCircle(t, 5, 10, 4, RED);
            DrawCircle(t, 11, 10, 4, RED);
            DrawTriangle(t, 8, 2, 1, 9, 15, 9, RED);
            // Highlight
            DrawCircle(t, 5, 11, 2, RED_L);
            SP(t, 4, 12, C(255, 170, 170));
            // Dark edge
            SP(t, 8, 3, RED_D); SP(t, 7, 3, RED_D); SP(t, 9, 3, RED_D);
            AutoOutline(t, BLK);
            Reg("ui_heart", t);
        }

        static void BuildUIStar()
        {
            var t = Tex(16, 16);
            // Central diamond
            DrawDiamond(t, 8, 8, 3, YELLOW);
            // Points
            DrawTriangle(t, 8, 14, 6, 10, 10, 10, YELLOW);
            DrawTriangle(t, 8, 2, 6, 6, 10, 6, YELLOW);
            DrawTriangle(t, 2, 8, 5, 6, 5, 10, YELLOW);
            DrawTriangle(t, 14, 8, 11, 6, 11, 10, YELLOW);
            // Diagonal fills
            DrawTriangle(t, 4, 13, 5, 10, 7, 11, GOLD);
            DrawTriangle(t, 12, 13, 9, 11, 11, 10, GOLD);
            DrawTriangle(t, 4, 3, 5, 6, 7, 5, GOLD);
            DrawTriangle(t, 12, 3, 9, 5, 11, 6, GOLD);
            SP(t, 8, 9, WHT); SP(t, 7, 8, YELLOW_L);
            AutoOutline(t, BLK);
            Reg("ui_star", t);
        }

        static void BuildUIArrow()
        {
            var t = Tex(16, 16);
            // Shaft
            DrawRect(t, 2, 6, 8, 4, WHT);
            // Arrowhead
            DrawTriangle(t, 14, 8, 9, 3, 9, 13, WHT);
            DrawLine(t, 9, 3, 14, 8, GRAY);
            DrawRect(t, 2, 6, 8, 1, GRAY_L);
            AutoOutline(t, BLK);
            Reg("ui_arrow", t);
        }

        static void BuildUIBtnNormal()
        {
            var t = Tex(64, 32);
            DrawRect(t, 2, 2, 60, 28, C(80, 80, 140));
            DrawRect(t, 3, 3, 58, 26, C(100, 100, 170));
            DrawRect(t, 3, 20, 58, 8, C(120, 120, 190));
            DrawRect(t, 3, 3, 58, 4, C(70, 70, 130));
            // Border
            DrawRect(t, 0, 0, 64, 1, C(50, 50, 90));
            DrawRect(t, 0, 31, 64, 1, C(50, 50, 90));
            DrawRect(t, 0, 0, 1, 32, C(50, 50, 90));
            DrawRect(t, 63, 0, 1, 32, C(50, 50, 90));
            // Round corners
            SP(t, 0, 0, CLR); SP(t, 63, 0, CLR);
            SP(t, 0, 31, CLR); SP(t, 63, 31, CLR);
            Reg("ui_btn_normal", t);
        }

        static void BuildUIBtnPressed()
        {
            var t = Tex(64, 32);
            DrawRect(t, 2, 2, 60, 28, C(60, 60, 110));
            DrawRect(t, 3, 3, 58, 26, C(70, 70, 130));
            DrawRect(t, 3, 3, 58, 8, C(90, 90, 150));
            DrawRect(t, 3, 20, 58, 8, C(55, 55, 100));
            DrawRect(t, 0, 0, 64, 1, C(40, 40, 70));
            DrawRect(t, 0, 31, 64, 1, C(40, 40, 70));
            DrawRect(t, 0, 0, 1, 32, C(40, 40, 70));
            DrawRect(t, 63, 0, 1, 32, C(40, 40, 70));
            SP(t, 0, 0, CLR); SP(t, 63, 0, CLR);
            SP(t, 0, 31, CLR); SP(t, 63, 31, CLR);
            Reg("ui_btn_pressed", t);
        }

        static void BuildUISlot()
        {
            var t = Tex(32, 32);
            DrawRect(t, 0, 0, 32, 32, C(60, 60, 80));
            DrawRect(t, 2, 2, 28, 28, C(40, 40, 55));
            // Inner highlight (top-left light, bottom-right shadow)
            DrawRect(t, 2, 29, 28, 1, C(80, 80, 100));
            DrawRect(t, 2, 2, 1, 28, C(80, 80, 100));
            DrawRect(t, 2, 2, 28, 1, C(30, 30, 45));
            DrawRect(t, 29, 2, 1, 28, C(30, 30, 45));
            SP(t, 1, 1, C(70, 70, 90)); SP(t, 30, 1, C(70, 70, 90));
            SP(t, 1, 30, C(70, 70, 90)); SP(t, 30, 30, C(70, 70, 90));
            Reg("ui_slot", t);
        }

        static void BuildUIJoystickBG()
        {
            var t = Tex(128, 128);
            DrawCircleOutline(t, 64, 64, 60, CA(40, 40, 60, 120), CA(80, 80, 120, 180));
            DrawCircleOutline(t, 64, 64, 56, CLR, CA(70, 70, 100, 100));
            DrawCircle(t, 64, 64, 4, CA(100, 100, 140, 150));
            // Cross-hair guides
            DrawLine(t, 64, 10, 64, 118, CA(80, 80, 110, 50));
            DrawLine(t, 10, 64, 118, 64, CA(80, 80, 110, 50));
            // Cardinal arrows
            DrawTriangle(t, 64, 118, 60, 112, 68, 112, CA(150, 150, 190, 100));
            DrawTriangle(t, 64, 10, 60, 16, 68, 16, CA(150, 150, 190, 100));
            DrawTriangle(t, 10, 64, 16, 60, 16, 68, CA(150, 150, 190, 100));
            DrawTriangle(t, 118, 64, 112, 60, 112, 68, CA(150, 150, 190, 100));
            Reg("ui_joystick_bg", t);
        }

        static void BuildUIJoystickHandle()
        {
            var t = Tex(48, 48);
            // Gradient-like concentric circles
            DrawCircle(t, 24, 24, 20, CA(120, 120, 170, 220));
            DrawCircle(t, 24, 24, 16, CA(140, 140, 190, 230));
            DrawCircle(t, 24, 24, 10, CA(160, 160, 210, 240));
            // Highlight
            DrawCircle(t, 20, 28, 6, CA(180, 180, 230, 200));
            DrawCircle(t, 19, 29, 3, CA(210, 210, 255, 180));
            // Outline ring
            for (int a = 0; a < 360; a++)
            {
                float rad = a * Mathf.Deg2Rad;
                int px = 24 + Mathf.RoundToInt(Mathf.Cos(rad) * 20);
                int py = 24 + Mathf.RoundToInt(Mathf.Sin(rad) * 20);
                SP(t, px, py, CA(80, 80, 120, 240));
            }
            Reg("ui_joystick_handle", t);
        }
    }
}
