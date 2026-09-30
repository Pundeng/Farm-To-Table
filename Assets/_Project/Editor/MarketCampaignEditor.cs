using CozyFoodFactory.Food;
using UnityEditor;
using UnityEngine;

namespace CozyFoodFactory.Editor
{
    [CustomEditor(typeof(Market))]
    public sealed class MarketCampaignEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            string error = ((Market)target).CampaignObjectiveError;
            if (!string.IsNullOrEmpty(error))
                EditorGUILayout.HelpBox(error, MessageType.Error);
            else
                EditorGUILayout.HelpBox(
                    "The Main Campaign Objectives array is the campaign sequence. " +
                    "Edit Food and Quantity inside each Requirements entry; " +
                    "drag entries to change their order. Keep shipped IDs and " +
                    "earlier objectives in place when existing saves must load.",
                    MessageType.Info);
        }
    }
}
