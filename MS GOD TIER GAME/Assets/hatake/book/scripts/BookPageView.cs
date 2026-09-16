using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// <summary>
// BookPageViewは、BookPageDataの内容をUI上に表示するためのコンポーネント

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(RectMask2D))]
[RequireComponent(typeof(Image))]

public sealed class BookPageView : MonoBehaviour
{
    private readonly List<GameObject> m_imageObjects =
        new List<GameObject>();

    public void ShowPage(BookPageData pageData)
    {
        ClearImages();

        RectTransform pageTransform = (RectTransform)transform;

        pageTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            BookPageData.PageSize.x);

        pageTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            BookPageData.PageSize.y);

        Image backgroundImage = GetComponent<Image>();

        backgroundImage.sprite = null;
        backgroundImage.color = pageData != null
            ? pageData.BackgroundColor
            : Color.white;

        backgroundImage.raycastTarget = true;

        if (pageData == null)
        {
            return;
        }

        foreach (BookImageData imageData in pageData.Images)
        {
            if (imageData == null || imageData.Sprite == null)
            {
                continue;
            }

            GameObject imageObject = new GameObject(
                imageData.Sprite.name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            imageObject.transform.SetParent(transform, false);

            // 撮影Cameraの対象Layerに揃えます。
            imageObject.layer = gameObject.layer;

            m_imageObjects.Add(imageObject);

            RectTransform imageTransform =
                imageObject.GetComponent<RectTransform>();

            // 画像の基準点を左上に統一
            imageTransform.anchorMin = new Vector2(0f, 1f);
            imageTransform.anchorMax = new Vector2(0f, 1f);
            imageTransform.pivot = new Vector2(0f, 1f);

            // 保存データのYは下向き、UI座標のYは上向き
            imageTransform.anchoredPosition = new Vector2(
                imageData.Position.x,
                -imageData.Position.y);

            imageTransform.sizeDelta = imageData.Size;

            Image image = imageObject.GetComponent<Image>();

            image.sprite = imageData.Sprite;
            image.material = imageData.Material;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }
    }

    private void ClearImages()
    {
        foreach (GameObject imageObject in m_imageObjects)
        {
            if (imageObject == null)
            {
                continue;
            }

            // Destroyの実行前に表示を止めます
            imageObject.SetActive(false);
            Destroy(imageObject);
        }

        m_imageObjects.Clear();
    }
}