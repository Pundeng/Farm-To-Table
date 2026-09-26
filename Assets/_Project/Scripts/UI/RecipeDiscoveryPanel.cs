using System;
using System.Collections.Generic;
using FantasyShapez.Buildings;
using FantasyShapez.Food;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyShapez.UI
{
    public sealed class RecipeDiscoveryPopupQueue
    {
        private readonly Queue<DiscoveredRecipe> pending = new();

        public DiscoveredRecipe Current { get; private set; }

        public void Show(DiscoveredRecipe recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (Current == null)
            {
                Current = recipe;
            }
            else
            {
                pending.Enqueue(recipe);
            }
        }

        public void Dismiss()
        {
            Current = pending.Count > 0 ? pending.Dequeue() : null;
        }
    }

    [RequireComponent(typeof(BuildingPlacementController))]
    public sealed class RecipeDiscoveryPanel : MonoBehaviour
    {
        private readonly RecipeDiscoveryPopupQueue popups = new();
        private BuildingPlacementController controller;
        private Vector2 bookScroll;
        private bool bookOpen;
        public bool HasModal => popups.Current != null;
        public bool DismissModal()
        {
            if (popups.Current == null) return false;
            popups.Dismiss();
            return true;
        }

        public bool BlocksWorldInput
        {
            get
            {
                if (!isActiveAndEnabled || Mouse.current == null)
                {
                    return false;
                }

                if (popups.Current != null)
                {
                    return true;
                }

                Vector2 pointer = Mouse.current.position.ReadValue();
                pointer.y = Screen.height - pointer.y;
                return controller != null && controller.IsFoodDemo
                    ? controller.OpenDemoPanel == BuildingPlacementController.DemoPanel.Recipe &&
                      GetBookRect().Contains(pointer)
                    : GetBookButtonRect().Contains(pointer) ||
                      (bookOpen && GetBookRect().Contains(pointer));
            }
        }

        public static string FormatRequirements(DiscoveredRecipe recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            return recipe.Kind == DiscoveredRecipeKind.Cutting
                ? $"Ingredient: {recipe.IngredientA.Id}; yields 2 {recipe.Output.Id}"
                : recipe.Kind == DiscoveredRecipeKind.Processing
                ? $"Ingredient: {recipe.IngredientA.Id}; Property: {recipe.Property}"
                : $"Ingredients: {recipe.IngredientA.Id} + {recipe.IngredientB.Id}";
        }

        private void OnEnable()
        {
            controller = GetComponent<BuildingPlacementController>();
            controller.RecipeDiscovered += popups.Show;
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.RecipeDiscovered -= popups.Show;
            }
        }

        private void OnGUI()
        {
            if (controller == null)
            {
                return;
            }

            if (popups.Current != null)
            {
                DrawPopup(popups.Current);
                return;
            }

            if (controller.IsFoodDemo)
            {
                if (controller.OpenDemoPanel == BuildingPlacementController.DemoPanel.Recipe)
                    DrawBook();
                return;
            }

            if (bookOpen)
            {
                DrawBook();
            }

            if (GUI.Button(GetBookButtonRect(),
                    $"Recipe Book ({controller.DiscoveredRecipes.Count})"))
            {
                bookOpen = !bookOpen;
            }
        }

        private void DrawPopup(DiscoveredRecipe recipe)
        {
            var rect = new Rect((Screen.width - 400f) * 0.5f,
                (Screen.height - 220f) * 0.5f, 400f, 220f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 16f, rect.y + 12f,
                rect.width - 32f, rect.height - 24f));
            GUILayout.Label("NEW RECIPE DISCOVERED!");
            GUILayout.Space(8f);
            GUILayout.Label(recipe.Output.Id);
            GUILayout.Label(FormatRequirements(recipe));
            GUILayout.Label($"Produces: {recipe.Output.Id}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Continue"))
            {
                popups.Dismiss();
            }

            GUILayout.EndArea();
        }

        private void DrawBook()
        {
            Rect rect = GetBookRect();
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 8f,
                rect.width - 24f, rect.height - 16f));
            GUILayout.Label("Recipe Book");
            bookScroll = GUILayout.BeginScrollView(bookScroll);
            IReadOnlyList<DiscoveredRecipe> recipes = controller.DiscoveredRecipes;
            if (recipes.Count == 0)
            {
                GUILayout.Label("No recipes discovered yet.");
            }

            foreach (DiscoveredRecipe recipe in recipes)
            {
                GUILayout.Label(recipe.Output.Id);
                GUILayout.Label(FormatRequirements(recipe));
                GUILayout.Label($"Produces: {recipe.Output.Id}");
                GUILayout.Space(8f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static Rect GetBookButtonRect() =>
            new(Mathf.Max(8f, Screen.width - 188f),
                Mathf.Max(8f, Screen.height - 48f), 172f, 32f);

        private Rect GetBookRect()
        {
            if (controller == null || !controller.IsFoodDemo)
                return new Rect(Mathf.Max(8f, Screen.width - 340f),
                    Mathf.Max(8f, Screen.height - 408f), 324f,
                    Mathf.Min(344f, Mathf.Max(100f, Screen.height - 72f)));
            float height = Mathf.Min(344f, Mathf.Max(80f, Screen.height - 130f));
            return new Rect(Mathf.Max(8f, Screen.width - 340f),
                Mathf.Max(8f, Screen.height - height - 114f),
                Mathf.Min(324f, Screen.width - 16f), height);
        }
    }
}
