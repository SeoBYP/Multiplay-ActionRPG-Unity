using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public static class MonoBehaviourExtensions
    {
        public static T GetOrAddComponent<T>(this MonoBehaviour behaviour) where T : Component
        {
            T component = behaviour.GetComponent<T>();
            if (component == null)
            {
                component = behaviour.gameObject.AddComponent<T>();
            }

            return component;
        }

        public static T GetAroundComponent<T>(this MonoBehaviour behaviour)
        {
            return GetAroundComponent<T>(behaviour.gameObject);
        }

        public static T GetAroundComponent<T>(this Transform behaviour)
        {
            return GetAroundComponent<T>(behaviour.gameObject);
        }

        public static T GetAroundComponent<T>(this GameObject behaviour)
        {
            T component = behaviour.GetComponentInParent<T>();
            if (component == null)
                component = behaviour.GetComponentInChildren<T>();
            if (component == null)
                component = behaviour.gameObject.GetComponent<T>();
            return component;
        }

        public static GameObject FindChildComponentByName(
            this MonoBehaviour behaviour,
            string objectName,
            bool includeInactive = true,
            bool ignoreCase = false)
        {
            return behaviour == null
                ? null
                : behaviour.gameObject.FindChildComponentByName<Transform>(objectName, includeInactive, ignoreCase).gameObject;
        }

        
        public static T FindChildComponentByName<T>(
            this MonoBehaviour behaviour,
            string objectName,
            bool includeInactive = true,
            bool ignoreCase = false) where T : Component
        {
            return behaviour == null
                ? null
                : behaviour.gameObject.FindChildComponentByName<T>(objectName, includeInactive, ignoreCase);
        }

        public static T FindChildComponentByName<T>(
            this Transform transform,
            string objectName,
            bool includeInactive = true,
            bool ignoreCase = false) where T : Component
        {
            return transform == null
                ? null
                : transform.gameObject.FindChildComponentByName<T>(objectName, includeInactive, ignoreCase);
        }

        public static T FindChildComponentByName<T>(
            this GameObject gameObject,
            string objectName,
            bool includeInactive = true,
            bool ignoreCase = false) where T : Component
        {
            if (gameObject == null || string.IsNullOrWhiteSpace(objectName))
                return null;

            StringComparison comparison = ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            T[] components = gameObject.GetComponentsInChildren<T>(includeInactive);

            foreach (T component in components)
            {
                if (component == null)
                    continue;

                if (string.Equals(component.gameObject.name, objectName, comparison))
                    return component;
            }

            return null;
        }

        public static T FindChildComponentByPath<T>(
            this MonoBehaviour behaviour,
            string path) where T : Component
        {
            return behaviour == null
                ? null
                : behaviour.transform.FindChildComponentByPath<T>(path);
        }

        public static T FindChildComponentByPath<T>(
            this GameObject gameObject,
            string path) where T : Component
        {
            return gameObject == null
                ? null
                : gameObject.transform.FindChildComponentByPath<T>(path);
        }

        public static T FindChildComponentByPath<T>(
            this Transform transform,
            string path) where T : Component
        {
            if (transform == null || string.IsNullOrWhiteSpace(path))
                return null;

            Transform target = transform.Find(path);
            if (target == null)
                return null;

            return target.GetComponent<T>();
        }

        public static List<T> FindChildComponentsContainsName<T>(
            this MonoBehaviour behaviour,
            string nameKeyword,
            bool includeInactive = true,
            bool ignoreCase = false) where T : Component
        {
            return behaviour == null
                ? new List<T>()
                : behaviour.gameObject.FindChildComponentsContainsName<T>(nameKeyword, includeInactive, ignoreCase);
        }

        public static List<T> FindChildComponentsContainsName<T>(
            this Transform transform,
            string nameKeyword,
            bool includeInactive = true,
            bool ignoreCase = false) where T : Component
        {
            return transform == null
                ? new List<T>()
                : transform.gameObject.FindChildComponentsContainsName<T>(nameKeyword, includeInactive, ignoreCase);
        }

        public static List<T> FindChildComponentsContainsName<T>(
            this GameObject gameObject,
            string nameKeyword,
            bool includeInactive = true,
            bool ignoreCase = false) where T : Component
        {
            List<T> results = new();

            if (gameObject == null || string.IsNullOrWhiteSpace(nameKeyword))
                return results;

            StringComparison comparison = ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            T[] components = gameObject.GetComponentsInChildren<T>(includeInactive);

            foreach (T component in components)
            {
                if (component == null)
                    continue;

                if (component.gameObject.name.IndexOf(nameKeyword, comparison) >= 0)
                    results.Add(component);
            }

            return results;
        }
    }
}