using UnityEngine;

public class DebugPageInteraction : IPageInteraction
{
    private readonly string m_pageName;

    public bool IsActive { get; private set; }

    public DebugPageInteraction(string pageName)
    {
        m_pageName = pageName;
    }

    public void Begin()
    {
        IsActive = true;

        Debug.Log(
            $"Page Interaction Begin : {m_pageName}");
    }

    public void End()
    {
        IsActive = false;

        Debug.Log(
            $"Page Interaction End : {m_pageName}");
    }
}