using System;
using System.Collections.Generic;

namespace Game.Gameplay.Character
{
    public abstract class State
    {
        protected List<ITransitionRule> _transitionRules = new();
        
        public event Action<StateKind> OnTransition;
        
        public abstract void Enter();

        public void Update(float deltaTime)
        {
            if (ShouldTransition(deltaTime))
                return;
            StateUpdate(deltaTime);
        }
        
        public abstract void Exit();
        
        protected abstract void StateUpdate(float deltaTime);
        
        private bool ShouldTransition(float deltaTime)
        {
            foreach (ITransitionRule rule in _transitionRules)
            {
                if (rule.ShouldTransition(deltaTime))
                {
                    OnTransition?.Invoke(rule.NextState);
                    return true;
                }
            }
            return false;
        }
        
        public void AddTransition(ITransitionRule rule)
        {
            _transitionRules.Add(rule);
        }
    }
}