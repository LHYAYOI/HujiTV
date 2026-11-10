using UnityEngine;

public sealed class RuneBulletPreviewCatalog : ScriptableObject
{
    [SerializeField] private MagicBulletController[] m_bullets;

    public MagicBulletController Find(MagicData magic)
    {
        if (magic == null || m_bullets == null) return null;
        foreach (MagicBulletController bullet in m_bullets)
            if (bullet != null && bullet.GetMagicData() == magic) return bullet;
        return null;
    }
}
