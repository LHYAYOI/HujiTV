using UnityEngine;

public interface IHitable
{
    public void OnHit(HitData hitData);
    public void OnStay(HitData hitData);
    public void OnExit(HitData hitData);
}
