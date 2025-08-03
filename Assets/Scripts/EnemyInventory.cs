using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

public class EnemyInventory : MonoBehaviour
{
    public int maxItems = 5;
    private int currentItems = 0;

    public int health;
    List<Item> items = new List<Item>();
    
    void Start()
    {

    }


    void Update()
    {
        //Reverse ForEach to add the Health to the enemy
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] is HealthItem healthItem)
            {
                health += healthItem.healthAmount;
                items.RemoveAt(i);
                currentItems--;
            }
        }
    }
    void OnCollisionStay(Collision collision)
    {
        Debug.Log("Collision detected with: " + collision.gameObject.name);
        if (collision.gameObject.CompareTag("Item"))
        {
            Item item = collision.gameObject.GetComponent<Item>();
            if (item != null)
            {
                AddItem(item);
                Destroy(collision.gameObject);
            }
        }
    }
    public void AddItem(Item item)
    {
        if (currentItems < maxItems)
        {
            currentItems++;
            items.Add(item);
            Debug.Log("Item added. Current items: " + currentItems);
        }
        else
        {
            Debug.Log("Inventory full. Cannot add more items.");
        }
    }

}
