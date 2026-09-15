using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BookPageData))]
public sealed class BookPageDataEditor : Editor
{
    private int m_selectedIndex = -1;
    private int m_dragControlId;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("m_pageName"));

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("m_backgroundColor"));

        SerializedProperty imagesProperty =
            serializedObject.FindProperty("m_images");

        EditorGUILayout.PropertyField(imagesProperty, true);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "画像をドラッグして配置",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "サイズは画像リスト内で編集します。" +
            "Materialの効果はこのプレビューには反映されません。",
            MessageType.Info);

        Vector2 pageSize = BookPageData.PageSize;

        Rect previewRect = GUILayoutUtility.GetAspectRect(
            pageSize.x / pageSize.y);

        DrawPreview(previewRect, imagesProperty);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPreview(
        Rect previewRect,
        SerializedProperty imagesProperty)
    {
        int controlId = GUIUtility.GetControlID(
            FocusType.Passive);

        float scale =
            previewRect.width / BookPageData.PageSize.x;

        Color backgroundColor = serializedObject
            .FindProperty("m_backgroundColor")
            .colorValue;

        EditorGUI.DrawRect(previewRect, backgroundColor);

        // ページ外への描画をクリップ
        GUI.BeginGroup(previewRect);

        for (int imageIndex = 0;
             imageIndex < imagesProperty.arraySize;
             imageIndex++)
        {
            SerializedProperty imageProperty =
                imagesProperty.GetArrayElementAtIndex(imageIndex);

            Sprite sprite = imageProperty
                .FindPropertyRelative("m_sprite")
                .objectReferenceValue as Sprite;

            Rect imageRect = GetImageRect(
                imageProperty,
                scale);

            if (sprite != null)
            {
                DrawSprite(imageRect, sprite);
            }

            if (imageIndex == m_selectedIndex)
            {
                GUI.Box(imageRect, GUIContent.none);
            }
        }

        GUI.EndGroup();

        Event inputEvent = Event.current;

        if (inputEvent.type == EventType.MouseDown &&
            inputEvent.button == 0 &&
            previewRect.Contains(inputEvent.mousePosition))
        {
            Vector2 mousePosition =
                inputEvent.mousePosition - previewRect.position;

            m_selectedIndex = -1;

            // 手前に表示されている画像から選択します。
            for (int imageIndex = imagesProperty.arraySize - 1;
                 imageIndex >= 0;
                 imageIndex--)
            {
                SerializedProperty imageProperty =
                    imagesProperty.GetArrayElementAtIndex(imageIndex);

                if (imageProperty
                    .FindPropertyRelative("m_sprite")
                    .objectReferenceValue == null)
                {
                    continue;
                }

                Rect imageRect = GetImageRect(
                    imageProperty,
                    scale);

                if (imageRect.Contains(mousePosition))
                {
                    m_selectedIndex = imageIndex;
                    break;
                }
            }

            if (m_selectedIndex >= 0)
            {
                m_dragControlId = controlId;
                GUIUtility.hotControl = controlId;
            }

            inputEvent.Use();
            Repaint();
        }
        else if (inputEvent.type == EventType.MouseDrag &&
                 GUIUtility.hotControl == controlId &&
                 m_selectedIndex >= 0 &&
                 m_selectedIndex < imagesProperty.arraySize)
        {
            SerializedProperty imageProperty =
                imagesProperty.GetArrayElementAtIndex(
                    m_selectedIndex);

            SerializedProperty positionProperty =
                imageProperty.FindPropertyRelative("m_position");

            positionProperty.vector2Value +=
                inputEvent.delta / scale;

            inputEvent.Use();
            Repaint();
        }
        else if (inputEvent.type == EventType.MouseUp &&
                 GUIUtility.hotControl == controlId)
        {
            GUIUtility.hotControl = 0;
            m_dragControlId = 0;

            inputEvent.Use();
        }
    }

    private Rect GetImageRect(
        SerializedProperty imageProperty,
        float scale)
    {
        Vector2 position = imageProperty
            .FindPropertyRelative("m_position")
            .vector2Value;

        Vector2 size = imageProperty
            .FindPropertyRelative("m_size")
            .vector2Value;

        size.x = Mathf.Max(1f, size.x);
        size.y = Mathf.Max(1f, size.y);

        return new Rect(
            position * scale,
            size * scale);
    }

    private void DrawSprite(Rect imageRect, Sprite sprite)
    {
        Texture2D texture = sprite.texture;

        if (texture == null)
        {
            return;
        }

        Rect spriteRect = sprite.rect;

        Rect textureCoordinates = new Rect(
            spriteRect.x / texture.width,
            spriteRect.y / texture.height,
            spriteRect.width / texture.width,
            spriteRect.height / texture.height);

        GUI.DrawTextureWithTexCoords(
            imageRect,
            texture,
            textureCoordinates,
            true);
    }

    private void OnDisable()
    {
        if (m_dragControlId != 0 &&
            GUIUtility.hotControl == m_dragControlId)
        {
            GUIUtility.hotControl = 0;
        }
    }
}