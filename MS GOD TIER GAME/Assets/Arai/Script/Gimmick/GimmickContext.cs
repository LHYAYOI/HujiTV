using System;
using UnityEngine;

public class GimmickContext
{
    public Action<SplineController2,float> SwitchSpline { get; }
    public Action<float,float> SetCartSpeed { get; }
    public CartController Cart { get; set; }


    public GimmickContext(
        CartController cart,
        Action<SplineController2, float> switchSpline,
        Action<float, float> setSpeed)
    {
        Cart = cart;
        SwitchSpline = switchSpline;
        SetCartSpeed = setSpeed;
    }
}
