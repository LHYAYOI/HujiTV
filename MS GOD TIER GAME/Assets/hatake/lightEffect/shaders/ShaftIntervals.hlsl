
#ifndef LIGHT_SHAFT_INTERVALS_INCLUDED
#define LIGHT_SHAFT_INTERVALS_INCLUDED

// すべての区間は、正規化されたワールド空間のカメラ光線に沿った距離
// サンプリングを行う前に、不透明な深度に対してクリッピングを行なわないと予期しない問題が出る可能性あり

float2 ShaftEmptyInterval() { return float2(1e20, -1e20); }

float2 ShaftSphereInterval(float3 origin, float3 ray, float radius, float limit)
{
    float b = dot(origin, ray);
    float discriminant = b*b - dot(origin, origin) + radius*radius;
    if (discriminant <= 0) return ShaftEmptyInterval();
    float root = sqrt(discriminant);
    return float2(max(0, -b-root), min(limit, -b+root));
}

float2 ShaftConeInterval(float3 origin, float3 ray, float3 axis, float cosine, float range, float limit)
{
    float2 interval = ShaftSphereInterval(origin, ray, range, limit);
    float axialOrigin = dot(origin, axis), axialRay = dot(ray, axis);
    
    // 円錐が被った時前方半分のみを発光
    if (abs(axialRay) < 1e-6) { if (axialOrigin < 0) return ShaftEmptyInterval(); }
    else if (axialRay > 0) interval.x = max(interval.x, -axialOrigin/axialRay);
    else interval.y = min(interval.y, -axialOrigin/axialRay);
    if (interval.y <= interval.x) return ShaftEmptyInterval();
    float c2 = cosine*cosine;
    float a = axialRay*axialRay-c2;
    float b = axialOrigin*axialRay-c2*dot(origin,ray);
    float c = axialOrigin*axialOrigin-c2*dot(origin,origin);
    if (abs(a) < 1e-6)
    {
        if (abs(b) < 1e-6) { if (c < 0) return ShaftEmptyInterval(); }
        else if (b > 0) interval.x = max(interval.x,-c/(2*b));
        else interval.y = min(interval.y,-c/(2*b));
    }
    else
    {
        float discriminant = b*b-a*c;
        if (discriminant < 0) { if (a < 0) return ShaftEmptyInterval(); }
        else
        {
            float root = sqrt(discriminant);
            float r0=(-b-root)/a, r1=(-b+root)/a;
            float enter=min(r0,r1), leave=max(r0,r1);
            if (a < 0) { interval.x=max(interval.x,enter); interval.y=min(interval.y,leave); }
            else if (interval.x < enter) interval.y=min(interval.y,enter);
            else interval.x=max(interval.x,leave);
        }
    }
    return interval;
}

float2 ShaftBoxInterval(float3 origin, float3 ray, float limit)
{
    float2 interval=float2(0,limit);
    [unroll] for(int axis=0;axis<3;axis++)
    {
        if(abs(ray[axis])<1e-7) { if(abs(origin[axis])>0.5) return ShaftEmptyInterval(); }
        else
        {
            float a=(-0.5-origin[axis])/ray[axis], b=(0.5-origin[axis])/ray[axis];
            interval.x=max(interval.x,min(a,b)); interval.y=min(interval.y,max(a,b));
        }
    }
    return interval;
}

float2 ShaftFogInterval(float3 origin, float3 ray, float4x4 worldToLocal, float shape, float limit)
{
    float3 localOrigin=mul(worldToLocal,float4(origin,1)).xyz;
    float3 localRay=mul((float3x3)worldToLocal,ray);
    if(shape<0.5) return ShaftBoxInterval(localOrigin,localRay,limit);
    float scale=length(localRay);
    if(scale<1e-7) return ShaftEmptyInterval();
    return ShaftSphereInterval(localOrigin,localRay/scale,0.5,limit*scale)/scale;
}

float ShaftRefineStep(float position, float stepSize, float2 interval, int minimumSamples)
{
    if(interval.y<=interval.x || position>=interval.y) return stepSize;
    if(position<interval.x) return min(stepSize,interval.x-position);
    return min(stepSize,min(interval.y-position,(interval.y-interval.x)/max(1,minimumSamples)));
}
#endif
