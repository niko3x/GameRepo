using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private List<HealthItem> healthItems = new List<HealthItem>();
    public float speed = 2f;
    public float healthThreshold = 50f; // The health threshold below which the enemy will seek health items
    void Start()
    {
        
    }


    void Update()
    {
        healthItems.Clear(); // Clear the list to gather current health items
        // Dynamically gather all the Items that are available in the scene by looking at their tags
        GameObject[] items = GameObject.FindGameObjectsWithTag("Item");
        //Loop through each item and check if it is a HealthItem
        foreach (GameObject item in items)
        {
            if (item.TryGetComponent<HealthItem>(out HealthItem healthItem))
            {
                healthItems.Add(healthItem);
            }
        }
        // Calculate the Distance to each HealthItem and then determine the closest one
        HealthItem closestItem = null;
        float closestDistance = Mathf.Infinity;
        foreach (HealthItem item in healthItems)
        {
            float distance = Vector3.Distance(transform.position, item.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestItem = item;
            }
        }
        // If a closest item is found, move towards it only if the health is below a certain threshold
        if (closestItem != null && GetComponent<EnemyInventory>().health < healthThreshold)
        {
            Vector3 direction = (closestItem.transform.position - transform.position).normalized;
            transform.position += direction * speed * Time.deltaTime;
            // Optionally, you can make the enemy face the item
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
        // If the enemy is not close enough to the item or the health is above the threshold it can move to the player
        else
        {
            // Logic to move towards the player or perform other actions
            // For example, you can find the player GameObject and move towards it
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Vector3 direction = (player.transform.position - transform.position).normalized;
                transform.position += direction * speed * Time.deltaTime;
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            }
        }
        

    }
}
