using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script.System.GamePlayAbilitySystem
{
    public class AbilitySystemComponent : MonoBehaviour
    {
        public List<GameplayAttribute> Attributes = new();

        private Dictionary<EGameplayAttribute, GameplayAttribute> _gameplayAttributes = new();

        private void Awake()
        {
            if (Attributes.Count == 0)
            {
                Attributes.Add(new GameplayAttribute(EGameplayAttribute.Health, 100, 100));
            }

            _gameplayAttributes.Clear();
            foreach (var attribute in Attributes)
            {
                attribute.Validate();
                _gameplayAttributes[attribute.AttributeType] = attribute;
            }
        }

        private void OnValidate()
        {
            foreach (var attribute in Attributes)
            {
                attribute.Validate();
            }
        }
        
        public GameplayAttribute GetAttribute(EGameplayAttribute attributeType)
        {
            return _gameplayAttributes.GetValueOrDefault(attributeType);
        }

        public bool TryGetAttribute(EGameplayAttribute attributeType, out GameplayAttribute attribute)
        {
            return _gameplayAttributes.TryGetValue(attributeType, out attribute);
        }
    }
}
