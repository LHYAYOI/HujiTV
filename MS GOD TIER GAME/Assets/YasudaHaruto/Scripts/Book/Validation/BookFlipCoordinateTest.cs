using UnityEngine;

namespace YasudaHaruto.BookValidation
{
public class BookFlipCoordinateTest : MonoBehaviour
{
    [SerializeField] private BookFlipCoordinateMapper m_mapper;
    [SerializeField] private BookPageAreaDetector m_areaDetector;

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 position = Input.mousePosition;

        if (m_mapper.TryGetCapturePosition(position, out BOOK_PAGE_SIDE side, out _))
            Debug.Log($"Book Side : {side}");
        else
            Debug.Log("Book Side : NONE");

        if (m_areaDetector.TryDetect(position, out BOOK_AREA_TYPE area, out Vector2 normalized, true))
            Debug.Log($"Book Area : {area} / Position : {normalized}");

        if (m_areaDetector.TryGetRunePosition(position, out Vector2 runePosition))
            Debug.Log($"Rune Position : {runePosition}");
    }
}
}
