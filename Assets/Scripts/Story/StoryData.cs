using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulCraft.Story
{
    /// <summary>
    /// 보스별 대사, 스테이지 인트로 대사, NPC 대화 데이터를 정적으로 관리한다.
    /// </summary>
    public static class StoryData
    {
        // ================================================================
        //  Boss Dialogue Data
        // ================================================================

        /// <summary>보스 등장 대사. Key = EnemyData.enemyId</summary>
        public static readonly Dictionary<string, BossDialogueSet> BossDialogues = new()
        {
            ["elder_grove"] = new BossDialogueSet
            {
                BossTitle = "숲의 파수꾼",
                BossDisplayName = "엘더 그로브",
                IntroLine = "이 숲을 더럽히는 자... 뿌리째 뽑아주마.",
                DeathLine = "숲은... 다시 자랄 것이다...",
                SkillRewardName = "자연의 포옹"
            },
            ["ignis"] = new BossDialogueSet
            {
                BossTitle = "용광로의 군주",
                BossDisplayName = "이그니스",
                IntroLine = "감히 나의 용광로에 발을 들이다니... 재가 되어라!",
                DeathLine = "이 화염이... 꺼진다고...?",
                SkillRewardName = "화염 폭풍"
            },
            ["glacia"] = new BossDialogueSet
            {
                BossTitle = "영겁의 빙결",
                BossDisplayName = "글라시아",
                IntroLine = "영원한 겨울 속에서 잠들어라...",
                DeathLine = "봄이... 오는 건가...",
                SkillRewardName = "절대영도"
            },
            ["voltar"] = new BossDialogueSet
            {
                BossTitle = "뇌운의 심판자",
                BossDisplayName = "볼타르",
                IntroLine = "하늘의 심판을 받아라, 미천한 그릇이여!",
                DeathLine = "번개여... 마지막 빛을...",
                SkillRewardName = "천공의 낙뢰"
            },
            ["malrok"] = new BossDialogueSet
            {
                BossTitle = "심연의 군주",
                BossDisplayName = "말로크",
                IntroLine = "드디어 왔군... 네 영혼으로 심연을 채워주마.",
                DeathLine = "불가능하다... 이 심연이... 흔들리다니...",
                SkillRewardName = "심연의 포옹"
            }
        };

        // ================================================================
        //  Stage Intro Narrations
        // ================================================================

        /// <summary>스테이지 인트로 내레이션. Key = stageId</summary>
        public static readonly Dictionary<string, StageNarration> StageNarrations = new()
        {
            ["stage_forest"] = new StageNarration
            {
                Title = "잊혀진 숲",
                Narration = "잊혀진 숲... 한때 정령들의 안식처였던 곳",
                SubText = "나뭇잎 사이로 불길한 기운이 스며든다."
            },
            ["stage_volcano"] = new StageNarration
            {
                Title = "멸화의 용광로",
                Narration = "끝없이 타오르는 용광로... 대지가 분노한다",
                SubText = "발밑의 열기가 점점 강해진다."
            },
            ["stage_glacier"] = new StageNarration
            {
                Title = "영겁의 빙하",
                Narration = "만년설이 뒤덮은 침묵의 땅... 모든 것이 얼어붙는다",
                SubText = "차가운 바람이 뼈를 파고든다."
            },
            ["stage_sky"] = new StageNarration
            {
                Title = "뇌운의 성채",
                Narration = "구름 위에 세워진 고대의 성채... 번개가 심판을 내린다",
                SubText = "천둥소리가 점점 가까워진다."
            },
            ["stage_abyss"] = new StageNarration
            {
                Title = "심연의 균열",
                Narration = "세계의 끝, 심연이 벌어진 곳... 돌아올 수 없을지도 모른다",
                SubText = "어둠 속에서 무언가가 속삭인다."
            }
        };

        // ================================================================
        //  NPC Dialogues
        // ================================================================

        /// <summary>NPC 대화 데이터. Key = npcId</summary>
        public static readonly Dictionary<string, DialogueLine[]> NpcDialogues = new()
        {
            ["blacksmith"] = new DialogueLine[]
            {
                new() { speakerName = "대장장이 볼드", text = "어서 오게. 새로운 장비가 필요한가?" },
                new() { speakerName = "대장장이 볼드", text = "몬스터에게서 얻은 소재가 있다면 장비를 강화해 줄 수 있지." },
                new() { speakerName = "대장장이 볼드", text = "조심하게. 앞으로의 길은 험난할 거야." }
            },
            ["elder"] = new DialogueLine[]
            {
                new() { speakerName = "장로 아이리스", text = "영혼의 그릇이여, 네가 왔구나." },
                new() { speakerName = "장로 아이리스", text = "이 세계의 균형이 무너지고 있다. 다섯 영혼의 수호자들이 타락했어." },
                new() { speakerName = "장로 아이리스", text = "그들을 쓰러뜨리고 영혼을 흡수해야만 세계를 되돌릴 수 있단다." },
                new() { speakerName = "장로 아이리스", text = "하지만 기억해라... 영혼을 흡수할수록 너 자신도 변할 것이다." }
            },
            ["merchant"] = new DialogueLine[]
            {
                new() { speakerName = "떠돌이 상인", text = "헤헤, 좋은 물건이 있지~" },
                new() { speakerName = "떠돌이 상인", text = "이 포션은 특별 할인이야. 한 번 보고 가라고!" }
            },
            ["tutorial_guide"] = new DialogueLine[]
            {
                new() { speakerName = "수호 정령", text = "영혼의 그릇이여, 나는 너를 인도할 수호 정령이다." },
                new() { speakerName = "수호 정령", text = "이 세계에서 살아남으려면 기본적인 전투 기술을 익혀야 해." },
                new() { speakerName = "수호 정령", text = "자, 내가 하나씩 알려줄게. 잘 따라와!" }
            }
        };

        // ================================================================
        //  Helper Methods
        // ================================================================

        /// <summary>
        /// 보스 ID로 대사 세트를 가져온다. 없으면 기본값을 반환한다.
        /// </summary>
        public static BossDialogueSet GetBossDialogue(string bossId)
        {
            if (!string.IsNullOrEmpty(bossId) && BossDialogues.TryGetValue(bossId, out var data))
                return data;

            return new BossDialogueSet
            {
                BossTitle = "???",
                BossDisplayName = "알 수 없는 존재",
                IntroLine = "...",
                DeathLine = "...",
                SkillRewardName = "미지의 힘"
            };
        }

        /// <summary>
        /// 스테이지 ID로 내레이션을 가져온다. 없으면 기본값을 반환한다.
        /// </summary>
        public static StageNarration GetStageNarration(string stageId)
        {
            if (!string.IsNullOrEmpty(stageId) && StageNarrations.TryGetValue(stageId, out var data))
                return data;

            return new StageNarration
            {
                Title = "미지의 영역",
                Narration = "알 수 없는 기운이 감돌고 있다...",
                SubText = ""
            };
        }

        /// <summary>
        /// NPC ID로 대화를 가져온다. 없으면 빈 배열을 반환한다.
        /// </summary>
        public static DialogueLine[] GetNpcDialogue(string npcId)
        {
            if (!string.IsNullOrEmpty(npcId) && NpcDialogues.TryGetValue(npcId, out var lines))
                return lines;

            return Array.Empty<DialogueLine>();
        }
    }

    // ================================================================
    //  Data Structures
    // ================================================================

    /// <summary>보스 대사 세트.</summary>
    [Serializable]
    public class BossDialogueSet
    {
        public string BossTitle;
        public string BossDisplayName;
        public string IntroLine;
        public string DeathLine;
        public string SkillRewardName;
    }

    /// <summary>스테이지 내레이션 데이터.</summary>
    [Serializable]
    public class StageNarration
    {
        public string Title;
        public string Narration;
        public string SubText;
    }
}
