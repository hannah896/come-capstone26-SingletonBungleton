using UnityEngine;

public class SearchableEnumAttribute : PropertyAttribute
{
    public int maxVisible = 15;
    public SearchableEnumAttribute() { }
    public SearchableEnumAttribute(int maxVisible)
    {
        this.maxVisible = maxVisible;
    }
}
