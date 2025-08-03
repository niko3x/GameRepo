using UnityEngine;

public class Item : MonoBehaviour
{
   
    
    public string itemName;
    public string description;
    public int value;

    public Item(string name, string desc, int val)
    {
        itemName = name;
        description = desc;
        value = val;
    }
        
    
}
