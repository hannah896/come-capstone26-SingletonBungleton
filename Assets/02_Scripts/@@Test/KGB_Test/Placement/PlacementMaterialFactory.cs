using UnityEngine;

public static class PlacementMaterialFactory
{
    public static Material CreateMaterial(Material template, Color color)
    {
        Shader shader = FindFallbackShader();
        Material material = template != null ? new Material(template) : new Material(shader);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

    // ?? 
    private static Shader FindFallbackShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
            return shader;

        shader = Shader.Find("Unlit/Transparent");
        if (shader != null)
            return shader;

        shader = Shader.Find("Unlit/Color");
        if (shader != null)
            return shader;

        return Shader.Find("Standard");
    }
}
