using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A signpost where a road leaves town: "Read" says where the road leads, and how dangerous the way is.
    public sealed class SignpostObject : Interactable
    {
        Signpost sign;
        string here;

        public override string Action => "Read";
        public override string DisplayName => "Signpost";
        public override string Examine => "It points the way to the next town.";

        public static SignpostObject Create(Signpost sign, string here, Transform parent)
        {
            var go = new GameObject("Signpost " + sign.At);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(sign.At);
            // The arm along the model's +x points the way the road goes.
            go.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-sign.DZ, sign.DX) * Mathf.Rad2Deg, 0f);
            ModelBuilder.Build(Landmarks.SignpostLook, go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.1f, 0f);
            box.size = new Vector3(0.5f, 2.2f, 0.5f);
            var post = go.AddComponent<SignpostObject>();
            post.Tile = sign.At;
            post.sign = sign;
            post.here = here;
            return post;
        }

        public override void Interact()
        {
            var ow = Game.Instance.World.Overworld;
            string way = sign.DX > 0 ? "East" : sign.DX < 0 ? "West" : sign.DZ > 0 ? "North" : "South";
            string back = sign.DX > 0 ? "West" : sign.DX < 0 ? "East" : sign.DZ > 0 ? "South" : "North";
            string to = OverworldBuilder.SettlementName(ow, sign.To) + (sign.To.City ? " (a city)" : "");
            // How deep into the wilderness the road goes, at its worst (sampled along the straight line).
            int worst = 0;
            for (int i = 1; i < 20; i++)
            {
                double f = i / 20.0;
                worst = Mathf.Max(worst, (int)ow.DangerAt(sign.At.X + (sign.To.Center.X - sign.At.X) * f, sign.At.Z + (sign.To.Center.Z - sign.At.Z) * f));
            }
            GameLog.Add("The signpost reads: " + way + ": " + to + ". " + back + ": " + here + ".");
            GameLog.Add(worst > 0 ? "Someone has scratched under it: \"Wilderness on the road, up to level " + worst + ". Travel armed.\""
                : "The road there is safe all the way.");
        }
    }
}
