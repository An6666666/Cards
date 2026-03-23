using System; // 提供 Serializable 等基礎 .NET 型別與屬性
using System.Collections.Generic; // 提供 List<T> 集合型別
using UnityEngine; // 提供 ScriptableObject、Sprite、Vector2 等 Unity 型別

// 定義商店教學中可被聚焦或高亮的 UI 錨點。
public enum ShopTutorialAnchor
{
    None, // 不指定教學焦點
    ShopTitle, // 商店標題文字
    GoldDisplay, // 顯示目前金幣的區域
    CardsTabButton, // 切換到卡牌分頁的按鈕
    RelicsTabButton, // 切換到遺物分頁的按鈕
    RemovalTabButton, // 切換到移除卡牌分頁的按鈕
    PreviousPageButton, // 上一頁按鈕
    NextPageButton, // 下一頁按鈕
    RefreshRemovalButton, // 重整移除列表的按鈕
    ReturnButton, // 返回地圖或離開商店的按鈕
    RemovalCost, // 顯示移除卡牌費用的文字
    CardList, // 卡牌商品列表區
    RelicList, // 遺物商品列表區
    RemovalList, // 可移除卡牌列表區
    CardsPanel, // 卡牌分頁面板
    RelicsPanel, // 遺物分頁面板
    RemovalPanel, // 移除卡牌分頁面板
    MessageText // 商店 NPC 或教學訊息文字區
}

// 定義商店教學流程中，玩家被要求執行的操作行為。
public enum ShopTutorialAction
{
    None, // 不要求玩家執行特定操作
    OpenCardsTab, // 要求玩家切換到卡牌分頁
    OpenRelicsTab, // 要求玩家切換到遺物分頁
    OpenRemovalTab, // 要求玩家切換到移除卡牌分頁
    PreviousPage, // 要求玩家切換到上一頁
    NextPage, // 要求玩家切換到下一頁
    RefreshRemoval, // 要求玩家刷新移除列表
    ReturnToRunMap // 要求玩家離開商店並返回跑圖場景
}

// 讓這份教學定義可以在 Unity 的 Create 選單中直接建立資產。
[CreateAssetMenu(fileName = "ShopTutorialDefinition", menuName = "Guide/Shop Tutorial Definition")]
public sealed class ShopTutorialDefinition : ScriptableObject
{
    // 表示商店教學中的單一步驟資料。
    [Serializable]
    public sealed class Step
    {
        [SerializeField] private string stepId; // 此步驟的唯一識別字串
        [SerializeField, TextArea(2, 5)] private string dialogue; // 此步驟顯示的主要對話內容
        [SerializeField, TextArea(1, 3)] private string prompt; // 此步驟顯示的操作提示文字
        [SerializeField] private ShopTutorialAnchor focusAnchor = ShopTutorialAnchor.None; // 此步驟要聚焦的 UI 錨點
        [SerializeField] private ShopTutorialAction requiredAction = ShopTutorialAction.None; // 此步驟要求玩家完成的操作
        [SerializeField] private bool selectTabBeforeStep; // 是否在進入此步驟前先自動切換分頁
        [SerializeField] private ShopUIManager.ShopTab tabToSelect = ShopUIManager.ShopTab.Cards; // 若要切頁，指定要切到哪個商店分頁
        [SerializeField] private Sprite tutorialImage; // 此步驟額外顯示的教學圖片
        [SerializeField, Range(0f, 1f)] private float tutorialImageAlpha = 1f; // 教學圖片透明度
        [SerializeField] private bool matchTargetRect = true; // 是否讓圖片尺寸對齊目標 UI 區域
        [SerializeField] private Vector2 imageSize = Vector2.zero; // 手動指定教學圖片尺寸
        [SerializeField] private Vector2 imageOffset = Vector2.zero; // 教學圖片相對目標的偏移量

        public string StepId => stepId; // 對外提供唯讀的步驟 ID
        public string Dialogue => dialogue; // 對外提供唯讀的對話文字
        public string Prompt => prompt; // 對外提供唯讀的提示文字
        public ShopTutorialAnchor FocusAnchor => focusAnchor; // 對外提供唯讀的焦點錨點
        public ShopTutorialAction RequiredAction => requiredAction; // 對外提供唯讀的必要操作
        public bool SelectTabBeforeStep => selectTabBeforeStep; // 對外提供唯讀的自動切頁設定
        public ShopUIManager.ShopTab TabToSelect => tabToSelect; // 對外提供唯讀的目標分頁
        public Sprite TutorialImage => tutorialImage; // 對外提供唯讀的教學圖片
        public float TutorialImageAlpha => tutorialImageAlpha; // 對外提供唯讀的圖片透明度
        public bool MatchTargetRect => matchTargetRect; // 對外提供唯讀的尺寸對齊設定
        public Vector2 ImageSize => imageSize; // 對外提供唯讀的圖片尺寸
        public Vector2 ImageOffset => imageOffset; // 對外提供唯讀的圖片偏移量
    }

    [SerializeField] private string completionFlag = GuideKeys.TutorialShopIntro; // 完成此教學後要記錄的存檔旗標
    [SerializeField] private List<Step> steps = new List<Step>(); // 商店教學包含的所有步驟資料

    // 對外提供安全的完成旗標讀取邏輯。
    public string CompletionFlag
    {
        get
        {
            // 若未設定旗標名稱，回退到預設的商店教學完成鍵值。
            if (string.IsNullOrWhiteSpace(completionFlag))
                return GuideKeys.TutorialShopIntro;

            // 去掉前後空白後再回傳，避免設定時誤輸入空格。
            return completionFlag.Trim();
        }
    }

    public IReadOnlyList<Step> Steps => steps; // 對外提供唯讀的教學步驟列表
    public bool HasSteps => steps != null && steps.Count > 0; // 快速判斷是否至少有一個教學步驟
}
