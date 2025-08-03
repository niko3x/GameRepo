using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float followSpeed = 2f; // Speed at which the camera follows the player
    public Vector3 offset; // Offset from the player's position
    public bool lookAtPlayer = true; // Whether the camera should look at the player
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Chech lookatPlayer
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Move the camera towards the player's position with an offset
            Vector3 targetPosition = player.transform.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

            // If lookAtPlayer is true, make the camera look at the player
            if (lookAtPlayer)
            {
                transform.LookAt(player.transform);
            }
        }
        else
        {
            // If no player then follow enemy
            GameObject enemy = GameObject.FindGameObjectWithTag("Enemy");
            if (enemy != null)
            {
                Vector3 targetPosition = player.transform.position + offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

                // If lookAtPlayer is true, make the camera look at the enemy
                if (!lookAtPlayer)
                {
                    transform.LookAt(enemy.transform);
                }
            }
        }

        


    
    }
}
