using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// A static authored dialogue graph, resolved by stable id from the data registry. Nodes are
    /// embedded; the graph never remembers the current node or which choices were taken (that lives
    /// in DialogueSessionRuntime / story state).
    [CreateAssetMenu(menuName = "JRPG/Dialogue/Dialogue Graph", fileName = "DialogueGraph")]
    public class DialogueGraphData : GameDataBase
    {
        public string entryNodeId;
        public DialogueImportance defaultImportance = DialogueImportance.Interactive;
        public List<DialogueNodeData> nodes = new();

        public DialogueNodeData FindNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;

            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].nodeId == nodeId) return nodes[i];
            
            return null;
        }
    }
}
