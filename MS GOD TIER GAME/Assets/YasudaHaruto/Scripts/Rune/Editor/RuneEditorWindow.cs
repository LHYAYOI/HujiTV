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
    private Rect m_drawingRect;

    private const float DRAWING_AREA_SIZE = 600f;
    private const float MIN_POINT_DISTANCE = 0.005f;

    private enum EDITOR_MODE
    {
        EDIT,
        TEST
    }

    private EDITOR_MODE m_mode = EDITOR_MODE.EDIT;

    private readonly List<Vector2> m_testCurrentStroke = new();
    private RuneTraceSession m_testSession = new();
    private RuneTraceResult m_testResult;

    private bool m_isTestDrawing;
    private bool m_showDebugVisualization = true;


    private float m_lineWidth = 15f;

    private const float MIN_LINE_WIDTH = 1f;
    private const float MAX_LINE_WIDTH = 20f;

    [MenuItem("Tools/Rune Editor")]
    public static void Open()
    {
        GetWindow<RuneEditorWindow>("Rune Editor");
    }


    private void OnGUI()
    {
        DrawRuneSelector();

        EditorGUILayout.Space(10);

        if (m_runeData == null)
        {
            EditorGUILayout.HelpBox("編集するRuneDataを選択してください。", MessageType.Info);

            return;
        }

        DrawModeSelector();

        DrawDisplaySettings();

        EditorGUILayout.Space(5);

        switch (m_mode)
        {
            case EDITOR_MODE.EDIT:

                DrawToolbar();

                EditorGUILayout.Space(5);

                DrawCanvas();

                HandleDrawingInput();

                DrawInfo();

                break;


            case EDITOR_MODE.TEST:

                if (m_runeData.TraceData.Strokes.Count == 0)
                {
                    EditorGUILayout.HelpBox("Bake済みのTraceDataがありません。Edit ModeでRuneをBakeしてください。", MessageType.Warning);

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


    private void DrawRuneSelector()
    {
        // RuneDataの選択フィールドを描画し、変更があった場合にm_runeDataを更新する
        EditorGUI.BeginChangeCheck();

        // ObjectFieldを使用してRuneDataを選択するUIを描画
        RuneData newRuneData = (RuneData)EditorGUILayout.ObjectField("Rune Data", m_runeData, typeof(RuneData), false);

        if (EditorGUI.EndChangeCheck())
        {
            m_runeData = newRuneData;

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
    }


    private void DrawCanvas()
    {
        // 描画エリアのサイズを計算する
        float width = Mathf.Min(position.width - 20f, DRAWING_AREA_SIZE);

        // 描画エリアの矩形を取得する
        m_drawingRect = GUILayoutUtility.GetRect(width, width, GUILayout.ExpandWidth(false));

        // 描画エリアの背景を描画する
        EditorGUI.DrawRect(m_drawingRect, new Color(0.15f, 0.15f, 0.15f));

        // 保存されたストロークを描画する
        DrawSavedStrokes();

        // Bakeされたストロークを描画する
        DrawBakedPoints();

        // 現在のストロークを描画する
        DrawCurrentStroke();
    }


    private void DrawSavedStrokes()
    {
        // RuneDataがnullの場合は処理を終了する
        if (m_runeData == null)
        {
            return;
        }

        // RuneDataに保存された各ストロークを描画する
        foreach (RuneStrokeData stroke in m_runeData.AuthoringData.Strokes)
        {
            DrawStroke(stroke.Points, Color.white);
        }
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

    private void HandleDrawingInput()
    {
        // 現在のイベントを取得する
        Event e = Event.current;

        // マウスが描画エリアの外にある場合、描画中であればストロークを終了し、処理を終了する
        if (!m_drawingRect.Contains(e.mousePosition))
        {
            if (m_isDrawing && e.type == EventType.MouseUp)
            {
                FinishStroke();
            }

            return;
        }

        // イベントの種類に応じて処理を分岐する
        switch (e.type)
        {
            // マウスボタンが押された場合の処理
            case EventType.MouseDown:

                if (e.button != 0)
                {// 左クリック以外の場合は処理を終了する
                    return;
                }

                // ストロークの描画を開始する
                StartStroke(e.mousePosition);

                // イベントを使用済みにする
                e.Use();

                break;

            // マウスがドラッグされた場合の処理
            case EventType.MouseDrag:

                if (!m_isDrawing || e.button != 0)
                {// 左クリック以外の場合または描画中でない場合は処理を終了する
                    return;
                }

                // 現在のマウス位置をストロークに追加する
                AddPoint(e.mousePosition);

                // イベントを使用済みにする
                e.Use();

                break;


            // マウスボタンが離された場合の処理
            case EventType.MouseUp:

                if (!m_isDrawing || e.button != 0)
                {// 左クリック以外の場合または描画中でない場合は処理を終了する
                    return;
                }

                // 現在のマウス位置をストロークに追加する
                AddPoint(e.mousePosition);

                // ストロークの描画を終了する
                FinishStroke();

                // イベントを使用済みにする
                e.Use();

                break;
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
        float y = (canvasPosition.y - m_drawingRect.y) / m_drawingRect.height;

        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }


    private Vector2 NormalizedToCanvas(Vector2 normalizedPosition)
    {
        // 正規化座標を描画エリアの左上を原点としたキャンバス座標に変換する
        float x = m_drawingRect.x + normalizedPosition.x * m_drawingRect.width;
        float y = m_drawingRect.y + normalizedPosition.y * m_drawingRect.height;

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

        m_mode = newMode;

        ResetCurrentDrawing();
    }

    private void DrawTestToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Reset Test"))
        {
            ResetTest();
        }

        if (GUILayout.Button("Evaluate"))
        {
            EvaluateTest();
        }

        EditorGUILayout.EndHorizontal();

        m_showDebugVisualization = EditorGUILayout.Toggle("Debug Visualization", m_showDebugVisualization);
    }

    private void DrawTestCanvas()
    {
        float width = Mathf.Min(position.width - 20f, DRAWING_AREA_SIZE);

        m_drawingRect = GUILayoutUtility.GetRect(width, width, GUILayout.ExpandWidth(false));

        EditorGUI.DrawRect(m_drawingRect, new Color(0.15f, 0.15f, 0.15f));

        DrawReferenceRune();

        DrawTestStrokes();

        DrawStroke(m_testCurrentStroke, Color.yellow);
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

        if (!m_drawingRect.Contains(e.mousePosition))
        {
            if (m_isTestDrawing && e.type == EventType.MouseUp)
            {
                FinishTestStroke();
            }

            return;
        }

        switch (e.type)
        {
            case EventType.MouseDown:

                if (e.button != 0)
                {
                    return;
                }

                StartTestStroke(e.mousePosition);

                e.Use();

                break;


            case EventType.MouseDrag:

                if (!m_isTestDrawing || e.button != 0)
                {
                    return;
                }

                AddTestPoint(e.mousePosition);

                e.Use();

                break;


            case EventType.MouseUp:

                if (!m_isTestDrawing || e.button != 0)
                {
                    return;
                }

                AddTestPoint(e.mousePosition);

                FinishTestStroke();

                e.Use();

                break;
        }
    }

    private void StartTestStroke(Vector2 mousePosition)
    {
        m_testCurrentStroke.Clear();

        m_isTestDrawing = true;

        AddTestPoint(mousePosition);
    }

    private void AddTestPoint(Vector2 mousePosition)
    {
        Vector2 normalizedPoint = CanvasToNormalized(mousePosition);

        if (m_testCurrentStroke.Count > 0)
        {
            Vector2 lastPoint = m_testCurrentStroke[m_testCurrentStroke.Count - 1];

            float distance = Vector2.Distance(lastPoint, normalizedPoint);

            if (distance < MIN_POINT_DISTANCE)
            {
                return;
            }
        }

        m_testCurrentStroke.Add(normalizedPoint);

        Repaint();
    }

    private void FinishTestStroke()
    {
        if (!m_isTestDrawing)
        {
            return;
        }

        m_isTestDrawing = false;

        if (m_testCurrentStroke.Count < 2)
        {
            m_testCurrentStroke.Clear();

            return;
        }

        m_testSession.AddStroke(m_testCurrentStroke);

        m_testCurrentStroke.Clear();

        // Stroke終了ごとに評価
        EvaluateTest();

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

        m_testResult = null;

        m_isTestDrawing = false;

        Repaint();
    }

    private void DrawTestResult()
    {
        EditorGUILayout.Space(5);

        if (m_testResult == null)
        {
            EditorGUILayout.LabelField("ルーンをなぞってください。");

            return;
        }

        RuneTraceRule rule = m_runeData.Rule;

        EditorGUILayout.LabelField($"Coverage : {m_testResult.Coverage:P1}");

        EditorGUILayout.LabelField($"Required : {rule.RequiredCoverage:P1}");

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField($"Accuracy : {m_testResult.Accuracy:P1}");

        EditorGUILayout.LabelField($"Required : {rule.RequiredAccuracy:P1}");

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField($"Strokes : {m_testResult.StrokeCount} / {rule.RequiredStrokeCount}");

        EditorGUILayout.Space(5);

        if (m_testResult.IsSuccess)
        {
            EditorGUILayout.HelpBox("SUCCESS", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("FAILED", MessageType.Warning);
        }
    }

    private void ResetCurrentDrawing()
    {
        m_currentStroke.Clear();
        m_testCurrentStroke.Clear();

        m_isDrawing = false;
        m_isTestDrawing = false;
    }
}