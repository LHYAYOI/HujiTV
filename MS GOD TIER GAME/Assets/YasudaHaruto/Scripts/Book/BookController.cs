//-----------------------------------------------
// BookController.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：ページめくりの制御を行うスクリプト
//-----------------------------------------------
using System;
using UnityEngine;

public class BookController
{
    private readonly BookModel m_model;
    private readonly BookInputController m_inputController;
    private readonly BookDisplayController m_displayController;

    public BookController(BookModel model, BookInputController inputController, BookDisplayController displayController)
    {
        m_model = model;
        m_inputController = inputController;
        m_displayController = displayController;
    }

    public void Begin()
    {
        BeginCurrentPageInteraction();
    }

    public void RequestNavigation(PAGE_NAVIGATION_DIRECTION direction)
    {
        Debug.Log($"Navigation Request : {direction} / Current={m_model.CurrentPageIndex} / CanNavigate={m_inputController.CanNavigatePage}");

        if (!m_inputController.CanNavigatePage)
        {
            return;
        }

        bool canMove = direction switch
        {
            PAGE_NAVIGATION_DIRECTION.NEXT => m_model.CanMoveNext,
            PAGE_NAVIGATION_DIRECTION.PREVIOUS => m_model.CanMovePrevious,
            _ => false
        };

        Debug.Log($"CanMove={canMove}");

        if (!canMove)
        {
            return;
        }

        EndCurrentPageInteraction();
        m_inputController.BeginPageTransition();

        bool moved = direction switch
        {
            PAGE_NAVIGATION_DIRECTION.NEXT => m_model.MoveNext(),
            PAGE_NAVIGATION_DIRECTION.PREVIOUS => m_model.MovePrevious(),
            _ => false
        };

        Debug.Log($"Moved={moved} / NewIndex={m_model.CurrentPageIndex}");

        if (!moved)
        {
            m_inputController.EndPageTransition();
            BeginCurrentPageInteraction();
            return;
        }

        bool forward = direction == PAGE_NAVIGATION_DIRECTION.NEXT;
        m_displayController.PlayTurn(m_model.CurrentPage.Data, forward, OnPageTransitionCompleted);
    }

    private void OnPageTransitionCompleted()
    {
        Debug.Log($"PageTransition Completed / Index={m_model.CurrentPageIndex}");

        m_inputController.EndPageTransition();
        BeginCurrentPageInteraction();
    }

    public void End()
    {
        EndCurrentPageInteraction();
    }

    private void BeginCurrentPageInteraction()
    {
        m_model.CurrentPage?.BeginInteraction();
    }

    private void EndCurrentPageInteraction()
    {
        m_model.CurrentPage?.EndInteraction();
    }
}