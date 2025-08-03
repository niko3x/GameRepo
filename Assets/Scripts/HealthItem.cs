using UnityEngine;
public class HealthItem : Item
{
    public int healthAmount;

    public HealthItem(string name, string desc, int val, int health) : base(name, desc, val)
    {
        healthAmount = health;
    }
}

