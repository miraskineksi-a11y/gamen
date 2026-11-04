using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Процедурная анимация «кукольных» персонажей из Blender (части тела — отдельные меши
    /// с центрами в суставах). Создаёт шарниры и двигает их: ходьба, бег, прыжок,
    /// удар саблей, блок, копание, штурвал, получение удара, смерть.
    /// </summary>
    public class HumanoidRig
    {
        public readonly Transform Model;
        public readonly Transform Hand, Sword;
        public readonly bool Valid;
        readonly Transform pBody, pHead, pArmL, pArmR, pLegL, pLegR;
        readonly Vector3 bodyRest;

        // входные параметры (задаёт владелец каждый кадр)
        public float moveSpeed, runSpeed = 7f;
        public bool grounded = true;
        public float attack = -1f;   // 0..1 — фаза удара, <0 — нет удара
        public bool block, steer, dead;
        public float dig = -1f;      // время копания, <0 — не копаем
        public float hurt;           // 0..1, затухает

        float phase, deadT, breathe;

        public HumanoidRig(Transform model)
        {
            Model = model;
            var body = Util.FindDeep(model, "Body");
            if (body == null) return;
            Valid = true;
            pBody = Pivot("BodyPivot", body, model);
            pLegL = Pivot("LegLPivot", Util.FindDeep(model, "LegL"), model);
            pLegR = Pivot("LegRPivot", Util.FindDeep(model, "LegR"), model);
            pHead = Pivot("HeadPivot", Util.FindDeep(model, "Head"), pBody);
            pArmL = Pivot("ArmLPivot", Util.FindDeep(model, "ArmL"), pBody);
            pArmR = Pivot("ArmRPivot", Util.FindDeep(model, "ArmR"), pBody);
            Sword = Util.FindDeep(model, "Sword");
            Hand = Util.FindDeep(model, "HandR");
            if (pArmR != null)
            {
                if (Sword != null) Sword.SetParent(pArmR, true);
                if (Hand != null) Hand.SetParent(pArmR, true);
            }
            bodyRest = pBody.localPosition;
        }

        Transform Pivot(string name, Transform part, Transform parent)
        {
            if (part == null || parent == null) return null;
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.position = part.position;
            p.rotation = Model.rotation;
            part.SetParent(p, true);
            return p;
        }

        static void Rot(Transform t, float x, float y, float z)
        {
            if (t != null) t.localRotation = Quaternion.Euler(x, y, z);
        }

        public void ResetPose()
        {
            dead = false;
            deadT = 0f;
            attack = -1f;
            dig = -1f;
            Model.localRotation = Quaternion.identity;
            Model.localPosition = Vector3.zero;
        }

        public void Tick(float dt)
        {
            if (!Valid) return;
            breathe += dt;
            if (dead)
            {
                deadT += dt;
                float k = Util.SmoothStep01(deadT / 0.6f);
                Model.localRotation = Quaternion.Euler(-86f * k, 0f, 0f);
                Model.localPosition = new Vector3(0f, 0.15f * k, 0f);
                Rot(pArmL, -150f * k, 0f, -40f * k);
                Rot(pArmR, -150f * k, 0f, 40f * k);
                Rot(pLegL, -10f * k, 0f, 0f);
                Rot(pLegR, 15f * k, 0f, 0f);
                return;
            }

            float sp = Mathf.Max(0f, moveSpeed);
            float sp01 = Mathf.Clamp01(sp / Mathf.Max(0.1f, runSpeed));
            phase += dt * (sp * 2.6f);
            float s = Mathf.Sin(phase);
            float swing = sp > 0.15f ? Mathf.Lerp(18f, 50f, sp01) : 0f;

            float legL = s * swing, legR = -s * swing;
            float armLx = -s * swing * 0.75f, armRx = s * swing * 0.75f;
            float armLz = -5f, armRz = 5f, armRy = 0f, armLy = 0f;
            float bob = Mathf.Abs(Mathf.Cos(phase)) * 0.06f * sp01;
            float lean = 7f * sp01;
            float twist = 0f;
            float idle = sp < 0.15f ? 1f : 0f;
            float br = Mathf.Sin(breathe * 2f) * idle;

            if (!grounded)
            {
                legL = -32f; legR = 22f;
                armLx = -30f; armRx = -30f; armLz = -28f; armRz = 28f;
            }
            if (steer)
            {
                legL = legR = 0f;
                armLx = -62f + Mathf.Sin(breathe * 1.3f) * 4f;
                armRx = -62f - Mathf.Sin(breathe * 1.3f) * 4f;
                armLz = 12f; armRz = -12f;
                lean = 4f;
            }
            if (dig >= 0f)
            {
                float d = Mathf.Sin(dig * 7f);
                armLx = -55f + d * 40f; armRx = -55f + d * 40f;
                armLz = 10f; armRz = -10f;
                lean = 22f + d * 10f;
                legL = -8f; legR = 8f;
            }
            if (block)
            {
                armRx = -78f; armRy = -40f; armRz = 10f;
                armLx = -55f; armLy = 35f;
                lean = Mathf.Max(lean, 6f);
            }
            if (attack >= 0f)
            {
                float t = attack, a;
                if (t < 0.32f) { float k = Util.SmoothStep01(t / 0.32f); a = Mathf.Lerp(0f, -155f, k); twist = -22f * k; armRz = 18f * k; }
                else if (t < 0.55f) { float k = Util.SmoothStep01((t - 0.32f) / 0.23f); a = Mathf.Lerp(-155f, 38f, k); twist = Mathf.Lerp(-22f, 26f, k); }
                else { float k = Util.SmoothStep01((t - 0.55f) / 0.45f); a = Mathf.Lerp(38f, 0f, k); twist = Mathf.Lerp(26f, 0f, k); }
                armRx = a;
                armRy = 0f;
                armLx = -28f; armLz = -22f;
                lean = Mathf.Max(lean, 8f);
            }
            float hurtLean = -18f * hurt;

            Rot(pLegL, legL, 0f, 0f);
            Rot(pLegR, legR, 0f, 0f);
            Rot(pArmL, armLx, armLy, armLz - br * 2f);
            Rot(pArmR, armRx, armRy, armRz + br * 2f);
            pBody.localPosition = bodyRest + new Vector3(0f, bob + br * 0.008f, 0f);
            pBody.localRotation = Quaternion.Euler(lean + hurtLean + br * 1.2f, twist, 0f);
            Rot(pHead, -lean * 0.5f - hurtLean * 0.3f, -twist * 0.4f, 0f);
        }
    }

    /// <summary>Анимация гигантского краба: ножки, клешни, смерть (переворачивается).</summary>
    public class CrabRig
    {
        readonly Transform model, body, clawL, clawR;
        readonly Transform[] legs = new Transform[6];
        readonly int[] legSide = new int[6];
        readonly Vector3 bodyRest;
        public readonly bool Valid;
        public float moveSpeed, attack = -1f, hurt;
        public bool dead;
        float phase, t, deadT;

        public CrabRig(Transform model)
        {
            this.model = model;
            var b = Util.FindDeep(model, "Body");
            if (b == null) return;
            Valid = true;
            body = Pivot(b, model);
            bodyRest = body.localPosition;
            clawL = Pivot(Util.FindDeep(model, "ClawL"), model);
            clawR = Pivot(Util.FindDeep(model, "ClawR"), model);
            int k = 0;
            foreach (var side in new[] { "L", "R" })
                for (int i = 1; i <= 3; i++)
                {
                    legs[k] = Pivot(Util.FindDeep(model, "Leg_" + side + i), model);
                    legSide[k] = side == "L" ? -1 : 1;
                    k++;
                }
        }

        Transform Pivot(Transform part, Transform parent)
        {
            if (part == null) return null;
            var p = new GameObject(part.name + "Pivot").transform;
            p.SetParent(parent, false);
            p.position = part.position;
            p.rotation = model.rotation;
            part.SetParent(p, true);
            return p;
        }

        public void Tick(float dt)
        {
            if (!Valid) return;
            t += dt;
            if (dead)
            {
                deadT += dt;
                float k = Util.SmoothStep01(deadT / 0.5f);
                model.localRotation = Quaternion.Euler(0f, 0f, 180f * k);
                model.localPosition = new Vector3(0f, 0.9f * k, 0f);
                for (int i = 0; i < 6; i++)
                    if (legs[i] != null) legs[i].localRotation = Quaternion.Euler(0f, 0f, legSide[i] * (25f + Mathf.Sin(t * 20f + i) * 15f * Mathf.Clamp01(2f - deadT)));
                return;
            }
            phase += dt * (moveSpeed * 5f + 0.6f);
            float amp = Mathf.Lerp(4f, 22f, Mathf.Clamp01(moveSpeed / 4f));
            for (int i = 0; i < 6; i++)
            {
                if (legs[i] == null) continue;
                float s = Mathf.Sin(phase + i * 2.1f + (legSide[i] > 0 ? Mathf.PI : 0f));
                legs[i].localRotation = Quaternion.Euler(0f, s * amp * 0.6f * legSide[i], legSide[i] * Mathf.Max(0f, s) * amp);
            }
            float snap = Mathf.Sin(t * 3f) * 6f;
            float lift = 0f;
            if (attack >= 0f)
            {
                lift = attack < 0.5f ? Mathf.Lerp(0f, -45f, attack / 0.5f) : Mathf.Lerp(-45f, 25f, (attack - 0.5f) / 0.5f);
                snap = 0f;
            }
            if (clawL != null) clawL.localRotation = Quaternion.Euler(lift + snap, 8f, 0f);
            if (clawR != null) clawR.localRotation = Quaternion.Euler(lift - snap, -8f, 0f);
            body.localPosition = bodyRest + new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 2f)) * 0.04f - hurt * 0.1f, 0f);
            body.localRotation = Quaternion.Euler(-hurt * 10f, 0f, Mathf.Sin(phase) * 3f);
        }
    }
}
