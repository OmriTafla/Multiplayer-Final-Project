// PolygonSDF.hlsl

void DrawPolygon_float(float2 UV, float Sides, float Thickness, float Scale, out float Out)
{
    // Remap UV from (0 to 1) to (-1 to 1)
    float2 pos = UV * 2.0 - 1.0;

    // Convert to polar coordinates
    float r = length(pos);
    float theta = atan2(pos.y, pos.x);

    float Math_PI = 3.14159265359;
    float safeSides = max(Sides, 3.0); 
    float angleSpacing = (2.0 * Math_PI) / safeSides;
    
    // Distance to the flat edge of the polygon
    float d = r * cos(theta - angleSpacing * floor((theta / angleSpacing) + 0.5));
    
    // Draw the outline centered exactly on the Scale value.
    // If Scale is 1.0, the flat edges will perfectly touch the UV cell boundaries.
    Out = smoothstep(Scale - Thickness, Scale, d) - smoothstep(Scale, Scale + Thickness, d);
}