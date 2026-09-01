using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "EnemyAIProfile", menuName = "Battle/Enemy AI Profile")]
public class EnemyAIProfileSO : ScriptableObject
{
    [Serializable]
    public class StrategyWeights
    {
        public float wDamage = 1f;
        public float wShield = 1f;
        public float wDraw = 1f;
        public float wHeal = 1f;
        public float wStatusBuff = 1f;
        public float wStatusDebuff = 1f;
        public float wEnergy = 1f;
        public float wDiscard = 1f;
        [FormerlySerializedAs("wPowerLongTerm")]
        public float wCoreLongTerm = 1f;
    }

    public BigFivePersonality defaultPersonality = new BigFivePersonality();

    public StrategyWeights aggressive = new StrategyWeights
    {
        wDamage = 1.9f,
        wShield = 0.5f,
        wDraw = 0.8f,
        wHeal = 0.6f,
        wStatusBuff = 0.9f,
        wStatusDebuff = 1.2f,
        wEnergy = 0.9f,
        wDiscard = 1.0f,
        wCoreLongTerm = 0.8f,
    };

    public StrategyWeights defensive = new StrategyWeights
    {
        wDamage = 0.9f,
        wShield = 1.8f,
        wDraw = 1.0f,
        wHeal = 1.6f,
        wStatusBuff = 0.9f,
        wStatusDebuff = 0.8f,
        wEnergy = 1.0f,
        wDiscard = 1.1f,
        wCoreLongTerm = 0.8f,
    };

    public StrategyWeights balanced = new StrategyWeights
    {
        wDamage = 1.25f,
        wShield = 1.2f,
        wDraw = 1.1f,
        wHeal = 1.0f,
        wStatusBuff = 1.0f,
        wStatusDebuff = 1.0f,
        wEnergy = 1.0f,
        wDiscard = 1.0f,
        wCoreLongTerm = 1.0f,
    };

    public StrategyWeights random = new StrategyWeights
    {
        wDamage = 1.0f,
        wShield = 0.9f,
        wDraw = 1.0f,
        wHeal = 0.9f,
        wStatusBuff = 1.0f,
        wStatusDebuff = 1.0f,
        wEnergy = 1.0f,
        wDiscard = 0.9f,
        wCoreLongTerm = 1.1f,
    };
}
