using UnityEditor;
using UnityEngine;

namespace YasudaHaruto.BookValidation.Editor
{
    public sealed class BookObstacleDebugWindow : EditorWindow
    {
        [SerializeField] private int m_slotCount = 1;
        private Vector2 m_scroll;


        [MenuItem("Tools/YasudaHaruto/Obstacle Debug Window")]
        public static void Open()
        {
            var window = GetWindow<BookObstacleDebugWindow>("お邪魔デバッグ");
            window.minSize = new Vector2(340, 260);
            window.Show();
        }


        private void OnInspectorUpdate() => Repaint();


        private void OnGUI()
        {
            m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
            try
            {
                DrawWindow();
            }
            finally { EditorGUILayout.EndScrollView(); }
        }


        private void DrawWindow()
        {
            EditorGUILayout.LabelField("インク汚れの追加", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play Mode中に本へ直接追加します。通信接続は不要です。", MessageType.Info);
            m_slotCount = EditorGUILayout.IntSlider("一度に埋める枠数", m_slotCount, 1, PageObstacleState.SLOT_COUNT);

            var bootstrap = Application.isPlaying ? Object.FindFirstObjectByType<BookGameBootstrap>() : null;
            var controller = bootstrap != null ? bootstrap.ObstacleController : null;
            var game = bootstrap != null ? bootstrap.GameController : null;
            var settings = controller != null ? controller.Settings : Resources.Load<ObstacleData>("BookObstacles/Rub");
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("m_requiredDistance"));
                var thresholds = serialized.FindProperty("m_clearOpacityThresholds");
                if (thresholds.arraySize != PageObstacleState.SLOT_COUNT)
                    thresholds.arraySize = PageObstacleState.SLOT_COUNT;
                EditorGUILayout.LabelField("薄くなったら完全消去", EditorStyles.boldLabel);
                for (int slot = 0; slot < PageObstacleState.SLOT_COUNT; slot++)
                {
                    var threshold = thresholds.GetArrayElementAtIndex(slot);
                    threshold.floatValue = EditorGUILayout.Slider((slot + 1) + "番枠：残り濃さ", threshold.floatValue, 0f, 0.95f);
                }
                EditorGUILayout.HelpBox("0.15なら残り15%で一気に消えます。大きいほど早く解除。0は最後まで擦ります。4番枠の飛沫は同時に消えます。", MessageType.None);
                serialized.ApplyModifiedProperties();
                EditorGUILayout.HelpBox("小さいほど早く消えます。2なら4の半分の距離で解除します。設定アセットへ保存されます。", MessageType.None);
                if (GUILayout.Button("画像・配置の設定を選択"))
                    Selection.activeObject = settings;
            }
            bool available = controller != null && game != null && game.IsInitialized && game.isActiveAndEnabled &&
                game.BookView != null && game.BookView.isActiveAndEnabled && !game.IsEditing &&
                !game.BookView.IsBusy && !game.BookView.FlipView.IsTurning && !EditorApplication.isPaused;

            using (new EditorGUI.DisabledScope(!available))
                if (GUILayout.Button("お邪魔を追加", GUILayout.Height(32)))
                    BookObstacleValidation.Preview((byte)m_slotCount);

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("BookSceneを開き、Play Modeにしてから追加してください。", MessageType.None);
            else if (!available)
                EditorGUILayout.HelpBox(bootstrap == null ? "BookGameBootstrapが見つかりません。" :
                    game == null ? "BookGameBootstrapのGame Controller参照が未設定です。" :
                    controller == null || !game.IsInitialized ? "本の初期化が完了していません。Consoleも確認してください。" :
                    EditorApplication.isPaused ? "Play Modeの一時停止を解除してください。" :
                    "本のめくり・編集の完了を待ってください。", MessageType.Info);
            else
                EditorGUILayout.LabelField("表紙・固定ページなら最初の通常見開きを開きます。", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            var page = game != null ? game.CurrentPage : null;
            EditorGUILayout.LabelField("現在の見開き", page != null ? page.Data.DisplayName : "通常見開き以外");
            if (page == null)
                return;
            for (int i = 0; i < PageObstacleState.SLOT_COUNT; i++)
            {
                var stain = page.Obstacles[i];
                EditorGUILayout.LabelField((i + 1) + "番枠", stain == null ? "空き" : "汚れあり／清掃 " + stain.Progress.ToString("P0"));
            }
            EditorGUILayout.LabelField("お邪魔による操作制限", page.Obstacles.BlocksActions ? "あり（4番枠）" : "なし");
        }
    }
}
