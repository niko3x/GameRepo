using UnityEngine;
using Unity.MLAgents;

public class TrainingManager : MonoBehaviour
{
    [Header("Episode Settings")]
    public float maxEpisodeTime = 30f;
    public bool autoResetEpisodes = true;
    
    private Agent[] allAgents;
    private float episodeStartTime;
    
    void Start()
    {
        allAgents = FindObjectsByType<Agent>(FindObjectsSortMode.None);
        episodeStartTime = Time.time;
    }
    
    void Update()
    {
        if (autoResetEpisodes)
        {
            // Check if episode should end
            if (Time.time - episodeStartTime > maxEpisodeTime)
            {
                ResetAllAgents();
            }
            
            // Check if only one agent remains
            int activeAgents = 0;
            foreach (var agent in allAgents)
            {
                if (agent != null && agent.gameObject.activeInHierarchy)
                {
                    activeAgents++;
                }
            }
            
            if (activeAgents <= 1)
            {
                ResetAllAgents();
            }
        }
    }
    
    public void ResetAllAgents()
    {
        Debug.Log("Resetting all agents");
        episodeStartTime = Time.time;
        
        foreach (var agent in allAgents)
        {
            if (agent != null)
            {
                agent.EndEpisode();
            }
        }
    }
}