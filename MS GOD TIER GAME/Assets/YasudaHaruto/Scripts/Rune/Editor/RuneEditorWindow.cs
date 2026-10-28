//-----------------------------------------------
// RuneEditorWindow.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：ルーンの編集用エディタウィンドウを表すクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RuneEditorWindow : EditorWindow
{
    private RuneData m_runeData;

    private readonly List<Vector2> m_currentStroke = new();

    private bool m_isDrawing;
    private enum CANVAS_TOOL { DRAW, SELECT }
    private CANVAS_TOOL m_canvasTool;
    private bool m_showSourceImage = true;
    private float m_sourceImageOpacity = 0.3f;
    private bool m_showImportedPreview = true;
    private int m_canvasControlId;

    private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        CancelCanvasInput();
    }
    private void OnLostFocus() => CancelCanvasInput();
    private void OnUndoRedo()
    {
        CancelCanvasInput();
        m_selectedStrokeIndex = -1;
        m_mergeTargetIndex = -1;
        ResetTest();
        Repaint();
    }
    private void CancelCanvasInput()
    {
        if (m_canvasControlId != 0 && GUIUtility.hotControl == m_canvasControlId)
            GUIUtility.hotControl = 0;
        m_isDrawing = false;
        m_currentStroke.Clear();
        Repaint();
    }

    private Rect m_drawingRect;

    private const float DRAWING_AREA_SIZE = 600f;
    private const float MIN_POINT_DISTANCE = 0.005f;

    private enum EDITOR_MODE
    {
        EDIT,
        TEST
    }

    private enum TEST_TRACE_STATE
    {
        WAITING_STROKE,
        DRAWING,
        WAITING_RELEASE,
        FAILED,
        SUCCESS
    }

    private TEST_TRACE_STATE m_testState = TEST_TRACE_STATE.WAITING_STROKE;
    private RuneTraceRealtimeEvaluator m_testEvaluator;
    private int m_testStrokeIndex;

    private EDITOR_MODE m_mode = EDITOR_MODE.EDIT;

    private readonly List<Vector2> m_testCurrentStroke = new();
    private RuneTraceSession m_testSession = new();
    private RuneTraceResult m_testResult;

    private bool m_isTestDrawing;
    private bool m_showDebugVisualization = true;


    private float m_lineWidth = 15f;

    private Texture2D m_sourceRuneImage;

    private bool[,] m_binaryRuneImage;

    private bool[,] m_skeletonRuneImage;

    private float m_imageThreshold = 0.3f;

    private bool m_showBinaryPreview;
    private bool m_showSkeletonPreview = true;

    private List<RuneStrokeData> m_importedRuneStrokes = new();

    private float m_simplifyTolerance = 0.002f;
    private float m_minImportedStrokeLength = 0.01f;

    private int m_extractedPointCount;

    private bool m_showStrokeGuide = true;

    private int m_selectedStrokeIndex = -1;
    private int m_mergeTargetIndex = -1;
    private int m_splitPointIndex = 1;
    private float m_mergeEndpointTolerance = 0.03f;

    private Vector2 m_scrollPosition;

    private static readonly Color START_POINT_COLOR = new(0.15f, 0.85f, 0.4f);
    private static readonly Color END_POINT_COLOR = new(1f, 0.3f, 0.3f);

    private const float MIN_SIMPLIFY_TOLERANCE = 0f;
    private const float MAX_SIMPLIFY_TOLERANCE = 0.02f;

    private const float MIN_IMAGE_THRESHOLD = 0f;
    private const float MAX_IMAGE_THRESHOLD = 1f;

    private const float MIN_LINE_WIDTH = 1f;
    private const float MAX_LINE_WIDTH = 20f;

    private const float TEST_SAMPLE_INTERVAL = 0.005f;

    [MenuItem("Tools/Rune Editor")]
    public static void Open()
    {
        GetWindow<RuneEditorWindow>("Rune Editor");
    }


    private void OnGUI()
    {
        m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);

        DrawRuneSelector();

        EditorGUILayout.Space(10);

        if (m_runeData == null)
        {
            EditorGUILayout.HelpBox("編集するRuneDataを選択してください。", MessageType.Info);
        }
        else
        {
            DrawModeSelector();
            DrawDisplaySettings();

            EditorGUILayout.Space(5);

            switch (m_mode)
            {
                case EDITOR_MODE.EDIT:
                    DrawToolbar();
                    DrawImageImportSettings();
                    DrawStrokeEditor();
                    DrawStrokeOperations();
                    DrawTraceRuleSettings();

                    EditorGUILayout.Space(5);

                    DrawCanvasTools();
                    DrawCanvas();
                    HandleDrawingInput();
                    DrawInfo();
                    break;

                case EDITOR_MODE.TEST:
                    if (m_runeData.TraceData.Strokes.Count == 0)
                    {
                        EditorGUILayout.HelpBox("Bake済みのTraceDataがありません。", MessageType.Warning);
                        break;
                    }

                    DrawTestToolbar();

                    EditorGUILayout.Space(5);

                    DrawTestCanvas();
                    HandleTestInput();
                    DrawTestResult();
                    break;
            }
        }

        EditorGUILayout.EndScrollView();
    }


    private void DrawRuneSelector()
    {
        // RuneDataの選択フィールドを描画し、変更があった場合にm_runeDataを更新する
        EditorGUI.BeginChangeCheck();

        // ObjectFieldを使用してRuneDataを選択するUIを描画
        RuneData newRuneData = (RuneData)EditorGUILayout.ObjectField("Rune Data", m_runeData, typeof(RuneData), false);

        if (EditorGUI.EndChangeCheck())
        {
            CancelCanvasInput();
            m_runeData = newRuneData;
            m_selectedStrokeIndex = -1;
            m_mergeTargetIndex = -1;
            m_splitPointIndex = 1;

            ResetCurrentDrawing();
            ResetTest();
        }
    }


    private void DrawToolbar()
    {
        // ツールバーの水平レイアウトを開始する
        EditorGUILayout.BeginHorizontal();

        // 「Undo Last Stroke」ボタンを描画し、クリックされた場合にUndoLastStrokeメソッドを呼び出す
        if (GUILayout.Button("Undo Last Stroke"))
        {
            UndoLastStroke();
        }

        // 「Clear」ボタンを描画し、クリックされた場合にClearRuneメソッドを呼び出す
        if (GUILayout.Button("Clear"))
        {
            ClearRune();
        }

        // 「Bake」ボタンを描画し、クリックされた場合にBakeRuneメソッドを呼び出す
        if (GUILayout.Button("Bake"))
        {
            BakeRune();
        }

        // ツールバーの水平レイアウトを終了する
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDisplaySettings()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Display Settings", EditorStyles.boldLabel);

        m_lineWidth = EditorGUILayout.Slider("Line Width", m_lineWidth, MIN_LINE_WIDTH, MAX_LINE_WIDTH);
        m_showStrokeGuide = EditorGUILayout.Toggle("Show Stroke Guide", m_showStrokeGuide);
    }


    private void DrawCanvas()
    {
        // 描画エリアのサイズを計算する
        float width = Mathf.Min(position.width - 20f, DRAWING_AREA_SIZE);

        // 描画エリアの矩形を取得する
        m_drawingRect = GUILayoutUtility.GetRect(width, width, GUILayout.ExpandWidth(false));

        m_canvasControlId = GUIUtility.GetControlID("RuneCanvas".GetHashCode(), FocusType.Keyboard, m_drawingRect);
        EditorGUI.DrawRect(m_drawingRect, new Color(0.15f, 0.15f, 0.15f));
        if (m_showSourceImage && m_sourceRuneImage != null && Event.current.type == EventType.Repaint)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, m_sourceImageOpacity);
            // Match the normalized 0..1 coordinates used by image extraction.
            GUI.DrawTexture(m_drawingRect, m_sourceRuneImage, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        if (m_showBinaryPreview)
        {
            DrawBinaryImagePreview();
        }

        if (m_showSkeletonPreview)
        {
            DrawSkeletonPreview();
        }

        if (m_showImportedPreview) DrawImportedStrokes();

        DrawSavedStrokes();

        DrawBakedPoints();

        DrawCurrentStroke();

        DrawStrokeGuides(m_runeData.AuthoringData.Strokes);
    }


    private void DrawSavedStrokes()
    {
        // RuneDataがnullの場合は処理を終了する
        if (m_runeData == null)
        {
            return;
        }

        // RuneDataに保存された各ストロークを描画する
        var strokes = m_runeData.AuthoringData.Strokes;
        for (int i = 0; i < strokes.Count; i++)
            DrawStroke(strokes[i].Points, i == m_selectedStrokeIndex ? Color.cyan : Color.white);
    }

    private void DrawBakedPoints()
    {
        if (m_runeData == null)
        {// RuneDataがnullの場合は処理を終了する
            return;
        }

        // GUI描画の開始
        Handles.BeginGUI();

        // 現在のハンドルカラーを保存する
        Color previousColor = Handles.color;

        // ハンドルカラーを緑色に設定する
        Handles.color = Color.green;

        // RuneDataのTraceDataに保存された各ストロークのポイントを描画する
        foreach (RuneStrokeData stroke in m_runeData.TraceData.Strokes)
        {
            foreach (Vector2 point in stroke.Points)
            {
                Vector2 canvasPoint = NormalizedToCanvas(point);

                Handles.DrawSolidDisc(canvasPoint, Vector3.forward, 2f);
            }
        }

        // ハンドルカラーを元に戻す
        Handles.color = previousColor;

        // GUI描画の終了
        Handles.EndGUI();
    }

    private void DrawCurrentStroke()
    {
        DrawStroke(m_currentStroke, Color.yellow);
    }


    private void DrawStroke(
    IReadOnlyList<Vector2> points,
    Color color)
    {
        if (points == null || points.Count < 2)
        {
            return;
        }

        Handles.BeginGUI();

        Color previousColor = Handles.color;
        Handles.color = color;

        Vector3[] canvasPoints = new Vector3[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            canvasPoints[i] = NormalizedToCanvas(points[i]);
        }

        Handles.DrawAAPolyLine(m_lineWidth, canvasPoints);

        Handles.color = previousColor;

        Handles.EndGUI();
    }

    private void DrawCanvasTools()
    {
        EditorGUILayout.LabelField("Canvas Editing", EditorStyles.boldLabel);
        var tool = (CANVAS_TOOL)GUILayout.Toolbar((int)m_canvasTool, new[] { "Draw", "Select / Delete" });
        if (tool != m_canvasTool) { CancelCanvasInput(); m_canvasTool = tool; }
        m_showSourceImage = EditorGUILayout.Toggle("Show Source Image", m_showSourceImage);
        m_sourceImageOpacity = EditorGUILayout.Slider("Source Opacity", m_sourceImageOpacity, 0f, 1f);
        m_showImportedPreview = EditorGUILayout.Toggle("Show Import Preview", m_showImportedPreview);
        EditorGUILayout.HelpBox(m_canvasTool == CANVAS_TOOL.DRAW
            ? "Draw over the source image. Each drag adds one stroke. Bake after editing."
            : "Click a saved stroke to select it (cyan). Delete removes it; Ctrl+Z restores it. Apply an import before editing its strokes.", MessageType.Info);
        using (new EditorGUI.DisabledScope(m_selectedStrokeIndex < 0 || m_selectedStrokeIndex >= m_runeData.AuthoringData.Strokes.Count))
            if (GUILayout.Button("Delete Selected Stroke")) DeleteSelectedStroke();
    }

    private void DeleteSelectedStroke()
    {
        int index = m_selectedStrokeIndex;
        EditStroke(() =>
        {
            var strokes = new List<RuneStrokeData>(m_runeData.AuthoringData.Strokes);
            if (index < 0 || index >= strokes.Count) return false;
            strokes.RemoveAt(index);
            m_runeData.AuthoringData.SetStrokes(strokes);
            return true;
        });
        m_selectedStrokeIndex = -1;
        m_mergeTargetIndex = -1;
    }

    private int FindStroke(Vector2 position)
    {
        float nearest = Mathf.Max(8f, m_lineWidth * 0.5f + 3f);
        int selected = -1;
        var strokes = m_runeData.AuthoringData.Strokes;
        for (int i = 0; i < strokes.Count; i++)
        {
            var points = strokes[i].Points;
            for (int j = 1; j < points.Count; j++)
            {
                Vector2 a = NormalizedToCanvas(points[j - 1]);
                Vector2 b = NormalizedToCanvas(points[j]);
                Vector2 segment = b - a;
                float t = segment.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(position - a, segment) / segment.sqrMagnitude) : 0f;
                float distance = Vector2.Distance(position, a + t * segment);
                if (distance >= nearest) continue;
                nearest = distance;
                selected = i;
            }
        }
        return selected;
    }

    private void HandleDrawingInput()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == m_canvasControlId)
        {
            if (e.keyCode == KeyCode.Escape) { CancelCanvasInput(); e.Use(); return; }
            if (m_canvasTool == CANVAS_TOOL.SELECT && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
            { DeleteSelectedStroke(); e.Use(); return; }
        }
        if (e.type == EventType.MouseDown && e.button == 0 && m_drawingRect.Contains(e.mousePosition))
        {
            GUIUtility.keyboardControl = m_canvasControlId;
            if (m_canvasTool == CANVAS_TOOL.SELECT)
            {
                m_selectedStrokeIndex = FindStroke(e.mousePosition);
                m_mergeTargetIndex = -1;
                Repaint();
            }
            else
            {
                GUIUtility.hotControl = m_canvasControlId;
                StartStroke(e.mousePosition);
            }
            e.Use();
        }
        else if (m_isDrawing && GUIUtility.hotControl == m_canvasControlId)
        {
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                // End at the boundary instead of connecting across an excursion outside the canvas.
                AddPoint(e.mousePosition);
                if (!m_drawingRect.Contains(e.mousePosition))
                { FinishStroke(); GUIUtility.hotControl = 0; }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                AddPoint(e.mousePosition);
                FinishStroke();
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }
    }


    private void StartStroke(Vector2 mousePosition)
    {
        // 現在のストロークをクリアする
        m_currentStroke.Clear();

        // 描画中フラグをtrueに設定する
        m_isDrawing = true;

        // 現在のマウス位置をストロークに追加する
        AddPoint(mousePosition);
    }


    private void AddPoint(Vector2 mousePosition)
    {
        // マウス位置を正規化座標に変換する
        Vector2 normalizedPoint = CanvasToNormalized(mousePosition);

        
        if (m_currentStroke.Count > 0)
        {// すでにストロークにポイントがある場合、最後のポイントとの距離を計算する

            Vector2 lastPoint = m_currentStroke[m_currentStroke.Count - 1];

            float distance = Vector2.Distance(lastPoint, normalizedPoint);

            if (distance < MIN_POINT_DISTANCE)
            {
                return;
            }
        }

        // 現在のマウス位置をストロークに追加する
        m_currentStroke.Add(normalizedPoint);

        // エディタウィンドウを再描画する
        Repaint();
    }


    private void FinishStroke()
    {
        if (!m_isDrawing)
        {// 描画中でない場合は処理を終了する
            return;
        }

        // 描画中フラグをfalseに設定する
        m_isDrawing = false;

        if (m_currentStroke.Count < 2)
        {// ストロークのポイントが2未満の場合は、現在のストロークをクリアして処理を終了する
            m_currentStroke.Clear();
            return;
        }

        // Undo操作を記録する
        Undo.RecordObject(m_runeData, "Add Rune Stroke");

        // 現在のストロークをRuneStrokeDataに変換する
        RuneStrokeData stroke = new RuneStrokeData(m_currentStroke);

        // RuneDataのAuthoringDataにストロークを追加する
        m_runeData.AuthoringData.AddStroke(stroke);
        m_runeData.TraceData.Clear();
        m_selectedStrokeIndex = m_runeData.AuthoringData.Strokes.Count - 1;

        // RuneDataを変更済みに設定する
        EditorUtility.SetDirty(m_runeData);

        // 現在のストロークをクリアする
        m_currentStroke.Clear();

        // エディタウィンドウを再描画する
        Repaint();
    }


    private Vector2 CanvasToNormalized(Vector2 canvasPosition)
    {
        // 描画エリアの左上を原点とした正規化座標に変換する
        float x = (canvasPosition.x - m_drawingRect.x) / m_drawingRect.width;
        float y = 1f - (canvasPosition.y - m_drawingRect.y) / m_drawingRect.height;


        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }


    private Vector2 NormalizedToCanvas(Vector2 normalizedPosition)
    {
        // 正規化座標を描画エリアの左上を原点としたキャンバス座標に変換する
        float x = m_drawingRect.x + normalizedPosition.x * m_drawingRect.width;
        float y = m_drawingRect.y + (1f - normalizedPosition.y) * m_drawingRect.height;

        return new Vector2(x, y);
    }


    private void UndoLastStroke()
    {
        if (m_runeData == null)
        {// RuneDataがnullの場合は処理を終了する
            return;
        }

        if (m_runeData.AuthoringData.Strokes.Count == 0)
        {// ストロークが存在しない場合は処理を終了する
            return;
        }

        // Undo操作を記録する
        Undo.RecordObject(m_runeData, "Remove Rune Stroke");

        // RuneDataのAuthoringDataから最後のストロークを削除する
        m_runeData.AuthoringData.RemoveLastStroke();
        m_runeData.TraceData.Clear();
        m_selectedStrokeIndex = -1;
        m_mergeTargetIndex = -1;

        // RuneDataを変更済みに設定する
        EditorUtility.SetDirty(m_runeData);

        // エディタウィンドウを再描画する
        Repaint();
    }


    private void ClearRune()
    {
        if (m_runeData == null)
        {// RuneDataがnullの場合は処理を終了する
            return;
        }

        // Undo操作を記録する
        Undo.RecordObject(m_runeData, "Clear Rune");

        // RuneDataのAuthoringDataをクリアする
        m_runeData.AuthoringData.Clear();
        m_selectedStrokeIndex = -1;
        m_mergeTargetIndex = -1;

        // RuneDataのTraceDataをクリアする
        m_runeData.TraceData.Clear();

        // RuneDataを変更済みに設定する
        EditorUtility.SetDirty(m_runeData);

        // 現在のストロークをクリアする
        m_currentStroke.Clear();

        // エディタウィンドウを再描画する
        Repaint();
    }

    private void BakeRune()
    {
        if (m_runeData == null)
        {// RuneDataがnullの場合は処理を終了する
            return;
        }

        // Undo操作を記録する
        Undo.RecordObject(m_runeData, "Bake Rune");

        // RuneDataのAuthoringDataをBakeしてTraceDataに設定する
        List<RuneStrokeData> bakedStrokes = RuneBaker.Bake(m_runeData.AuthoringData, RuneTraceSettings.SAMPLE_INTERVAL);

        // RuneDataのTraceDataにBakeされたストロークを設定する
        m_runeData.TraceData.SetStrokes(bakedStrokes);

        // RuneDataを変更済みに設定する
        EditorUtility.SetDirty(m_runeData);

        // 変更を保存する
        AssetDatabase.SaveAssets();

        // エディタウィンドウを再描画する
        Repaint();

        Debug.Log($"Rune Bake Complete : {m_runeData.name}");
    }

    private void DrawInfo()
    {
        EditorGUILayout.Space(5);

        int authoringPointCount = 0;

        foreach (RuneStrokeData stroke in m_runeData.AuthoringData.Strokes)
        {
            authoringPointCount += stroke.Points.Count;
        }

        int bakedPointCount = 0;

        foreach (RuneStrokeData stroke in m_runeData.TraceData.Strokes)
        {
            bakedPointCount += stroke.Points.Count;
        }

        EditorGUILayout.LabelField($"Strokes : {m_runeData.AuthoringData.Strokes.Count}");

        EditorGUILayout.LabelField($"Authoring Points : {authoringPointCount}");

        EditorGUILayout.LabelField($"Baked Points : {bakedPointCount}");
    }

    private void DrawModeSelector()
    {
        EDITOR_MODE newMode = (EDITOR_MODE)GUILayout.Toolbar((int)m_mode, new string[] { "Edit", "Test" });

        if (newMode == m_mode)
        {
            return;
        }

        CancelCanvasInput();
        m_mode = newMode;
        ResetCurrentDrawing();
        ResetTest();
    }


    private void DrawTestToolbar()
    {
        if (GUILayout.Button("Reset Test")) ResetTest();
    }

    private void DrawTestCanvas()
    {
        float width = Mathf.Min(position.width - 20f, DRAWING_AREA_SIZE);
        m_drawingRect = GUILayoutUtility.GetRect(width, width, GUILayout.ExpandWidth(false));

        EditorGUI.DrawRect(m_drawingRect, new Color(0.15f, 0.15f, 0.15f));

        foreach (RuneStrokeData stroke in m_runeData.TraceData.Strokes)
            DrawStroke(stroke.Points, Color.gray);

        foreach (RuneStrokeData stroke in m_testSession.Strokes)
            DrawStroke(stroke.Points, Color.green);

        DrawStroke(m_testCurrentStroke, Color.yellow);
        DrawStrokeGuides(m_runeData.TraceData.Strokes);
    }

    private void DrawTestResult()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"State : {m_testState}");
        EditorGUILayout.LabelField($"Stroke : {Mathf.Min(m_testStrokeIndex + 1, m_runeData.TraceData.Strokes.Count)} / {m_runeData.TraceData.Strokes.Count}");
        EditorGUILayout.LabelField($"Progress : {(m_testEvaluator?.Progress ?? 0f):P1}");

        if (m_testState == TEST_TRACE_STATE.SUCCESS)
            EditorGUILayout.HelpBox("SUCCESS - All strokes completed!", MessageType.Info);
        else if (m_testState == TEST_TRACE_STATE.FAILED)
            EditorGUILayout.HelpBox("FAILED - Click again to retry.", MessageType.Warning);
        else if (m_testState == TEST_TRACE_STATE.WAITING_RELEASE)
            EditorGUILayout.HelpBox("Stroke completed. Release to continue.", MessageType.Info);
    }


    private void DrawReferenceRune()
    {
        if (m_runeData == null)
        {
            return;
        }

        if (!m_showDebugVisualization || m_testSession.StrokeCount == 0)
        {
            foreach (RuneStrokeData stroke in m_runeData.TraceData.Strokes)
            {
                DrawStroke(stroke.Points, Color.gray);
            }

            return;
        }

        DrawCoverageDebug();
    }

    private void DrawCoverageDebug()
    {
        List<RuneStrokeData> evaluationStrokes = RunePathUtility.ResampleStrokes(m_testSession.Strokes, RuneTraceSettings.SAMPLE_INTERVAL);

        float tolerance = m_runeData.Rule.DistanceTolerance;

        foreach (RuneStrokeData stroke in m_runeData.TraceData.Strokes)
        {
            IReadOnlyList<Vector2> points = stroke.Points;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 startPoint = points[i];
                Vector2 endPoint = points[i + 1];

                bool startCovered = RunePathUtility.IsPointNearStrokes(startPoint, evaluationStrokes, tolerance);

                bool endCovered = RunePathUtility.IsPointNearStrokes(endPoint, evaluationStrokes, tolerance);

                Color color = startCovered && endCovered ? Color.green : Color.red;

                DrawSegment(startPoint, endPoint, color);
            }
        }
    }

    private void DrawSegment(Vector2 start, Vector2 end, Color color)
    {
        Handles.BeginGUI();

        Color previousColor = Handles.color;
        Handles.color = color;

        Vector3[] points = {NormalizedToCanvas(start), NormalizedToCanvas(end) };

        Handles.DrawAAPolyLine(m_lineWidth, points);

        Handles.color = previousColor;

        Handles.EndGUI();
    }

    private void DrawTestStrokes()
    {
        if (!m_showDebugVisualization)
        {
            foreach (RuneStrokeData stroke in m_testSession.Strokes)
            {
                DrawStroke(stroke.Points, Color.white);
            }

            return;
        }

        DrawAccuracyDebug();
    }

    private void DrawAccuracyDebug()
    {
        List<RuneStrokeData> evaluationStrokes = RunePathUtility.ResampleStrokes(m_testSession.Strokes, RuneTraceSettings.SAMPLE_INTERVAL);

        float tolerance = m_runeData.Rule.DistanceTolerance;

        foreach (RuneStrokeData stroke in m_testSession.Strokes)
        {
            IReadOnlyList<Vector2> points = stroke.Points;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 startPoint = points[i];
                Vector2 endPoint = points[i + 1];

                bool startAccurate = RunePathUtility.IsPointNearStrokes(startPoint, evaluationStrokes, tolerance);

                bool endAccurate = RunePathUtility.IsPointNearStrokes(endPoint, evaluationStrokes, tolerance);

                Color color = startAccurate && endAccurate ? Color.white : Color.yellow;

                DrawSegment(startPoint, endPoint, color);
            }
        }
    }


    private void HandleTestInput()
    {
        Event e = Event.current;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button != 0 || !m_drawingRect.Contains(e.mousePosition)) return;
                StartTestStroke(e.mousePosition);
                e.Use();
                break;

            case EventType.MouseDrag:
                if (e.button != 0 || !m_isTestDrawing) return;
                AddTestPoint(e.mousePosition);
                e.Use();
                break;

            case EventType.MouseUp:
                if (e.button != 0 || !m_isTestDrawing) return;
                if (m_testState == TEST_TRACE_STATE.DRAWING)
                    AddTestPoint(e.mousePosition);
                FinishTestStroke();
                e.Use();
                break;
        }
    }

    private void StartTestStroke(Vector2 mousePosition)
    {
        if (m_testState == TEST_TRACE_STATE.SUCCESS) return;
        if (m_testState == TEST_TRACE_STATE.FAILED) ResetTest();
        if (m_testState != TEST_TRACE_STATE.WAITING_STROKE) return;

        IReadOnlyList<Vector2> points = m_runeData.TraceData.Strokes[m_testStrokeIndex].Points;
        RuneTraceRule rule = m_runeData.Rule;

        m_testEvaluator = new RuneTraceRealtimeEvaluator(
            points, rule.DistanceTolerance, rule.StartTolerance,
            rule.EndTolerance, TEST_SAMPLE_INTERVAL);

        m_testCurrentStroke.Clear();
        m_isTestDrawing = true;

        Vector2 position = CanvasToNormalized(mousePosition);

        if (!m_testEvaluator.Begin(position))
        {
            m_testState = TEST_TRACE_STATE.FAILED;
            Repaint();
            return;
        }

        m_testCurrentStroke.Add(position);
        m_testState = TEST_TRACE_STATE.DRAWING;
        Repaint();
    }

    private void AddTestPoint(Vector2 mousePosition)
    {
        if (m_testState != TEST_TRACE_STATE.DRAWING) return;

        Vector2 position = CanvasToNormalized(mousePosition);
        Vector2 previous = m_testCurrentStroke[m_testCurrentStroke.Count - 1];

        if (Vector2.Distance(previous, position) < MIN_POINT_DISTANCE) return;

        RUNE_REALTIME_RESULT result = m_testEvaluator.EvaluateSegment(previous, position);

        if (result == RUNE_REALTIME_RESULT.FAILED)
        {
            m_testState = TEST_TRACE_STATE.FAILED;
            Repaint();
            return;
        }

        m_testCurrentStroke.Add(position);

        if (result == RUNE_REALTIME_RESULT.COMPLETED)
        {
            m_testSession.AddStroke(m_testCurrentStroke);

            bool isLast = m_testStrokeIndex == m_runeData.TraceData.Strokes.Count - 1;
            m_testState = isLast ? TEST_TRACE_STATE.SUCCESS : TEST_TRACE_STATE.WAITING_RELEASE;
        }

        Repaint();
    }

    private void FinishTestStroke()
    {
        m_isTestDrawing = false;

        switch (m_testState)
        {
            case TEST_TRACE_STATE.DRAWING:
                m_testState = TEST_TRACE_STATE.FAILED;
                break;

            case TEST_TRACE_STATE.WAITING_RELEASE:
                m_testStrokeIndex++;
                m_testEvaluator = null;
                m_testCurrentStroke.Clear();
                m_testState = TEST_TRACE_STATE.WAITING_STROKE;
                break;

            case TEST_TRACE_STATE.SUCCESS:
                m_testCurrentStroke.Clear();
                break;
        }

        Repaint();
    }


    private void EvaluateTest()
    {
        if (m_runeData == null)
        {
            return;
        }

        m_testResult = RuneTraceEvaluator.Evaluate(m_runeData, m_testSession);

        Repaint();
    }
    private void ResetTest()
    {
        m_testSession.Clear();
        m_testCurrentStroke.Clear();

        m_testEvaluator = null;
        m_testStrokeIndex = 0;
        m_testState = TEST_TRACE_STATE.WAITING_STROKE;
        m_isTestDrawing = false;

        Repaint();
    }

    private void ResetCurrentDrawing()
    {
        m_currentStroke.Clear();
        m_testCurrentStroke.Clear();

        m_isDrawing = false;
        m_isTestDrawing = false;
    }


    private void DrawImageImportSettings()
    {
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField(
            "Image Import",
            EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        m_sourceRuneImage =
            (Texture2D)EditorGUILayout.ObjectField(
                "Source Image",
                m_sourceRuneImage,
                typeof(Texture2D),
                false);
        if (EditorGUI.EndChangeCheck())
        {
            m_binaryRuneImage = null;
            m_skeletonRuneImage = null;
            m_importedRuneStrokes.Clear();
            m_extractedPointCount = 0;
        }

        m_imageThreshold =
            EditorGUILayout.Slider(
                "Threshold",
                m_imageThreshold,
                MIN_IMAGE_THRESHOLD,
                MAX_IMAGE_THRESHOLD);

        EditorGUILayout.HelpBox("Transparent images: alpha coverage. Opaque images: dark lines on a light background.", MessageType.Info);
        m_minImportedStrokeLength = EditorGUILayout.Slider("Min Stroke Length", m_minImportedStrokeLength, 0f, 0.05f);

        m_simplifyTolerance =
            EditorGUILayout.Slider(
                "Simplify Tolerance",
                m_simplifyTolerance,
                MIN_SIMPLIFY_TOLERANCE,
                MAX_SIMPLIFY_TOLERANCE);

        m_showBinaryPreview =
            EditorGUILayout.Toggle(
                "Show Binary",
                m_showBinaryPreview);

        m_showSkeletonPreview =
            EditorGUILayout.Toggle(
                "Show Skeleton",
                m_showSkeletonPreview);

        EditorGUILayout.Space(5);

        // -------------------------
        // Analyze
        // -------------------------

        EditorGUI.BeginDisabledGroup(
            m_sourceRuneImage == null);

        if (GUILayout.Button("Analyze Image"))
        {
            AnalyzeRuneImage();
        }

        EditorGUI.EndDisabledGroup();

        // -------------------------
        // Analyze Result
        // -------------------------

        if (m_importedRuneStrokes != null &&
            m_importedRuneStrokes.Count > 0)
        {
            int simplifiedPointCount = 0;

            foreach (RuneStrokeData stroke
                     in m_importedRuneStrokes)
            {
                simplifiedPointCount +=
                    stroke.Points.Count;
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField(
                $"Detected Strokes : {m_importedRuneStrokes.Count}");

            EditorGUILayout.LabelField(
                $"Points : {m_extractedPointCount} -> {simplifiedPointCount}");

            EditorGUILayout.Space(5);
        }

        // -------------------------
        // Apply
        // -------------------------

        EditorGUI.BeginDisabledGroup(
            m_runeData == null ||
            m_importedRuneStrokes == null ||
            m_importedRuneStrokes.Count == 0);

        if (GUILayout.Button("Apply to Rune"))
        {
            ApplyImportedRune();
        }

        EditorGUI.EndDisabledGroup();
    }


    private void DrawStrokeEditor()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Stroke Editor", EditorStyles.boldLabel);

        int count = m_runeData.AuthoringData.Strokes.Count;
        for (int i = 0; i < count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(i == m_selectedStrokeIndex, $"Stroke {i + 1}", "Button", GUILayout.Width(90)))
                m_selectedStrokeIndex = i;

            using (new EditorGUI.DisabledScope(i == 0))
            {
                if (GUILayout.Button("Up", GUILayout.Width(50)))
                {
                    EditStroke(() => m_runeData.AuthoringData.MoveStroke(i, i - 1));
                    m_selectedStrokeIndex = i - 1;
                    m_mergeTargetIndex = -1;
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(i == count - 1))
            {
                if (GUILayout.Button("Down", GUILayout.Width(50)))
                {
                    EditStroke(() => m_runeData.AuthoringData.MoveStroke(i, i + 1));
                    m_selectedStrokeIndex = i + 1;
                    m_mergeTargetIndex = -1;
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("Reverse", GUILayout.Width(75)))
            {
                EditStroke(() => m_runeData.AuthoringData.ReverseStroke(i));
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Delete", GUILayout.Width(55)))
            {
                m_selectedStrokeIndex = i;
                DeleteSelectedStroke();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
    }


    private void DrawStrokeOperations()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Stroke Operations", EditorStyles.boldLabel);

        var strokes = m_runeData.AuthoringData.Strokes;
        int count = strokes.Count;

        if (count == 0)
        {
            EditorGUILayout.HelpBox("No strokes available.", MessageType.Info);
            return;
        }

        string[] options = new string[count + 1];
        options[0] = "Select Stroke";

        for (int i = 0; i < count; i++)
            options[i + 1] = $"Stroke {i + 1} ({strokes[i].Points.Count} points)";

        m_selectedStrokeIndex = EditorGUILayout.Popup(
            "Selected Stroke", Mathf.Clamp(m_selectedStrokeIndex + 1, 0, count), options) - 1;

        if (m_selectedStrokeIndex < 0) return;

        int pointCount = strokes[m_selectedStrokeIndex].Points.Count;

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Split", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(pointCount < 3))
        {
            if (pointCount >= 3)
            {
                m_splitPointIndex = Mathf.Clamp(m_splitPointIndex, 1, pointCount - 2);
                m_splitPointIndex = EditorGUILayout.IntSlider("Split Point", m_splitPointIndex, 1, pointCount - 2);
            }

            if (GUILayout.Button("Split Stroke"))
            {
                int index = m_selectedStrokeIndex;
                int point = m_splitPointIndex;

                EditStroke(() => m_runeData.AuthoringData.SplitStroke(index, point));
                m_selectedStrokeIndex = -1;
                m_mergeTargetIndex = -1;
                GUIUtility.ExitGUI();
            }
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Merge", EditorStyles.boldLabel);

        m_mergeTargetIndex = EditorGUILayout.Popup(
            "Merge With", Mathf.Clamp(m_mergeTargetIndex + 1, 0, count), options) - 1;

        m_mergeEndpointTolerance = Mathf.Max(0f,
            EditorGUILayout.FloatField("Max Endpoint Distance", m_mergeEndpointTolerance));

        bool canMerge = m_mergeTargetIndex >= 0 && m_mergeTargetIndex != m_selectedStrokeIndex;

        using (new EditorGUI.DisabledScope(!canMerge))
        {
            if (GUILayout.Button("Merge Strokes"))
            {
                int first = m_selectedStrokeIndex;
                int second = m_mergeTargetIndex;
                float tolerance = m_mergeEndpointTolerance;

                if (CanMergeStrokes(first, second, tolerance))
                {
                    EditStroke(() => m_runeData.AuthoringData.MergeStrokes(first, second, tolerance));
                    m_selectedStrokeIndex = -1;
                    m_mergeTargetIndex = -1;
                    GUIUtility.ExitGUI();
                }
                else
                {
                    EditorUtility.DisplayDialog("Merge Failed",
                        "The selected strokes have no endpoints close enough to merge.", "OK");
                }
            }
        }
    }

    private bool CanMergeStrokes(int firstIndex, int secondIndex, float tolerance)
    {
        var strokes = m_runeData.AuthoringData.Strokes;

        if (firstIndex < 0 || firstIndex >= strokes.Count ||
            secondIndex < 0 || secondIndex >= strokes.Count ||
            firstIndex == secondIndex) return false;

        var a = strokes[firstIndex].Points;
        var b = strokes[secondIndex].Points;

        if (a.Count < 2 || b.Count < 2) return false;

        float minDistance = Mathf.Min(
            Vector2.Distance(a[0], b[0]),
            Vector2.Distance(a[0], b[b.Count - 1]),
            Vector2.Distance(a[a.Count - 1], b[0]),
            Vector2.Distance(a[a.Count - 1], b[b.Count - 1]));

        return minDistance <= tolerance;
    }


    private void DrawTraceRuleSettings()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Realtime Trace Settings", EditorStyles.boldLabel);

        RuneTraceRule rule = m_runeData.Rule;

        EditorGUI.BeginChangeCheck();

        float distance = EditorGUILayout.FloatField("Distance Tolerance", rule.DistanceTolerance);
        float start = EditorGUILayout.FloatField("Start Tolerance", rule.StartTolerance);
        float end = EditorGUILayout.FloatField("End Tolerance", rule.EndTolerance);

        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(m_runeData, "Change Rune Trace Settings");
        rule.SetTolerances(distance, start, end);
        EditorUtility.SetDirty(m_runeData);
    }

    private void EditStroke(System.Func<bool> editAction)
    {
        Undo.RecordObject(m_runeData, "Edit Rune Stroke");

        if (!editAction()) return;

        m_runeData.TraceData.Clear();
        EditorUtility.SetDirty(m_runeData);
        Repaint();
    }

    private void AnalyzeRuneImage()
    {
        if (m_sourceRuneImage == null)
        {
            return;
        }

        // 画像を二値化
        m_binaryRuneImage =
            RuneImagePreprocessor.Binarize(
                m_sourceRuneImage,
                m_imageThreshold);

        // 二値画像を細線化
        m_skeletonRuneImage =
            RuneSkeletonizer.Skeletonize(
                m_binaryRuneImage);

        // SkeletonからStrokeを抽出
        List<RuneStrokeData> extractedStrokes =
            RuneStrokeExtractor.Extract(
                m_skeletonRuneImage);

        // 抽出されたStrokeを簡略化
        m_importedRuneStrokes.Clear();
        m_extractedPointCount = 0;

        foreach (RuneStrokeData stroke in extractedStrokes)
        {
            float length = 0f;
            for (int i = 1; i < stroke.Points.Count; i++)
                length += Vector2.Distance(stroke.Points[i - 1], stroke.Points[i]);
            if (length < m_minImportedStrokeLength) continue;

            m_extractedPointCount += stroke.Points.Count;

            List<Vector2> simplifiedPoints =
                RunePathSimplifier.Simplify(
                    stroke.Points,
                    m_simplifyTolerance);

            if (simplifiedPoints.Count < 2)
            {
                continue;
            }

            m_importedRuneStrokes.Add(
                new RuneStrokeData(
                    simplifiedPoints));
        }

        m_showImportedPreview = true;
        Repaint();
    }

    private void DrawBinaryImagePreview()
    {
        if (m_binaryRuneImage == null)
        {
            return;
        }

        int width = m_binaryRuneImage.GetLength(0);
        int height = m_binaryRuneImage.GetLength(1);

        float pixelWidth = m_drawingRect.width / width;
        float pixelHeight = m_drawingRect.height / height;

        Color runeColor = new Color(0.4f, 0.4f, 0.4f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!m_binaryRuneImage[x, y])
                {
                    continue;
                }

                Rect pixelRect = new Rect(
                    m_drawingRect.x + x * pixelWidth,
                    m_drawingRect.y + (height - 1 - y) * pixelHeight,
                    pixelWidth,
                    pixelHeight);

                EditorGUI.DrawRect(pixelRect, runeColor);
            }
        }
    }

    private void DrawSkeletonPreview()
    {
        if (m_skeletonRuneImage == null)
        {
            return;
        }

        int width = m_skeletonRuneImage.GetLength(0);
        int height = m_skeletonRuneImage.GetLength(1);

        float pixelWidth = m_drawingRect.width / width;
        float pixelHeight = m_drawingRect.height / height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!m_skeletonRuneImage[x, y])
                {
                    continue;
                }

                Rect pixelRect = new Rect(
                    m_drawingRect.x + x * pixelWidth,
                    m_drawingRect.y + (height - 1 - y) * pixelHeight,
                    Mathf.Max(pixelWidth, 1f),
                    Mathf.Max(pixelHeight, 1f));

                EditorGUI.DrawRect(pixelRect, Color.cyan);
            }
        }
    }

    private void DrawImportedStrokes()
    {
        if (m_importedRuneStrokes == null)
        {
            return;
        }

        foreach (RuneStrokeData stroke
                 in m_importedRuneStrokes)
        {
            DrawStroke(
                stroke.Points,
                Color.magenta);
        }
    }

    private void ApplyImportedRune()
    {
        if (m_runeData == null)
        {
            return;
        }

        if (m_importedRuneStrokes == null ||
            m_importedRuneStrokes.Count == 0)
        {
            return;
        }

        // Undo操作を記録
        Undo.RecordObject(
            m_runeData,
            "Apply Imported Rune");

        // 解析結果をAuthoringDataに設定
        m_runeData.AuthoringData.SetStrokes(
            m_importedRuneStrokes);

        // 以前のBake結果は無効になるため削除
        m_runeData.TraceData.Clear();

        // RuneDataを変更済みに設定
        EditorUtility.SetDirty(m_runeData);

        m_showImportedPreview = false;
        m_showBinaryPreview = false;
        m_showSkeletonPreview = false;
        m_selectedStrokeIndex = -1;
        m_mergeTargetIndex = -1;
        Repaint();
    }


    private void DrawStrokeGuides(IReadOnlyList<RuneStrokeData> strokes)
    {
        if (!m_showStrokeGuide || strokes == null) return;

        Handles.BeginGUI();
        Color previousColor = Handles.color;

        for (int i = 0; i < strokes.Count; i++)
        {
            IReadOnlyList<Vector2> points = strokes[i].Points;
            if (points == null || points.Count < 2) continue;

            Vector2 start = NormalizedToCanvas(points[0]);
            Vector2 end = NormalizedToCanvas(points[points.Count - 1]);

            Handles.color = START_POINT_COLOR;
            Handles.DrawSolidDisc(start, Vector3.forward, 7f);

            Handles.color = END_POINT_COLOR;
            Handles.DrawSolidDisc(end, Vector3.forward, 7f);

            Handles.Label(start + new Vector2(10f, -22f), $"S{i + 1}", EditorStyles.whiteBoldLabel);
            Handles.Label(end + new Vector2(10f, 5f), $"E{i + 1}", EditorStyles.whiteBoldLabel);

            DrawStrokeDirection(points);
        }

        Handles.color = previousColor;
        Handles.EndGUI();
    }

    private void DrawStrokeDirection(IReadOnlyList<Vector2> points)
    {
        if (points.Count < 2) return;

        int index = Mathf.Max(0, (points.Count - 1) / 2);
        Vector2 start = NormalizedToCanvas(points[index]);
        Vector2 end = NormalizedToCanvas(points[index + 1]);

        Vector2 direction = (end - start).normalized;
        if (direction.sqrMagnitude < 0.0001f) return;

        Vector2 center = (start + end) * 0.5f;
        Vector2 tip = center + direction * 10f;
        Vector2 left = center - direction * 5f + new Vector2(-direction.y, direction.x) * 6f;
        Vector2 right = center - direction * 5f - new Vector2(-direction.y, direction.x) * 6f;

        Handles.color = Color.yellow;
        Handles.DrawAAPolyLine(2.5f, left, tip, right);
    }

}