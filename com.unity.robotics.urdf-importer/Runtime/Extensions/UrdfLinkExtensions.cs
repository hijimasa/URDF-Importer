/*
© Siemens AG, 2018
Author: Suzannah Smith (suzannah.smith@siemens.com)
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>.
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/  

using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfLinkExtensions
    {
        /// <summary>
        /// fixed 関節のリンクを親へ畳んで ArticulationBody を作らないようにするか。
        /// 従来どおり 1 リンク 1 ボディにしたい場合は false にする。
        /// </summary>
        public static bool MergeFixedLinks = true;

        public static GameObject Create(Transform parent, Link link = null, Joint joint = null)
        {
            GameObject linkObject = new GameObject("link");
            linkObject.transform.SetParentAndAlign(parent);
            UrdfLink urdfLink = linkObject.AddComponent<UrdfLink>();
            UrdfCollisionsExtensions.Create(linkObject.transform, link?.collisions);
            UrdfVisualsExtensions.Create(linkObject.transform, link?.visuals);

            if (link != null)
            {
                urdfLink.ImportLinkData(link, joint);
            }
            else
            {
                UrdfInertial.Create(linkObject);
#if UNITY_EDITOR
                UnityEditor.EditorGUIUtility.PingObject(linkObject);
#endif
            }

            return linkObject;
        }

        private static void ImportLinkData(this UrdfLink urdfLink, Link link, Joint joint)
        {
            if (link.inertial == null && joint == null)
            {
                urdfLink.IsBaseLink = true;
            }
            urdfLink.gameObject.name = link.name;
            if (joint?.origin != null)
                UrdfOrigin.ImportOriginData(urdfLink.transform, joint.origin);

            // fixed 関節のリンクは自由度を持たないので、ArticulationBody を作らずに
            // 質量・慣性を親へ畳む。PhysX の articulation は 1 階層 64 ボディが上限で、
            // 固定リンクにまでボディを作ると、センサフレームや装飾部品の多い URDF が
            // 上限に達して超過分が物理から抜け落ちるため (Unity は
            // "The maximum limit of ArticulationBody hierarchy size is reached" を出すが、
            // 読み込み自体は成功したように見えてしまう)。
            //
            // GameObject の階層・名前・transform は従来どおり残すので、リンク名で対象を
            // 探すセンサ取り付けや TF は影響を受けない。コライダーは Unity の規定どおり
            // 直近の祖先ボディに属し、剛結合として振る舞う。
            if (MergeFixedLinks
                && joint != null
                && UrdfJoint.GetJointType(joint.type) == UrdfJoint.JointTypes.Fixed
                && UrdfInertial.MergeIntoAncestorBody(urdfLink.gameObject, link.inertial))
            {
                return;
            }

            if (link.inertial != null)
            {
                UrdfInertial.Create(urdfLink.gameObject, link.inertial);

                if (joint != null)
                    UrdfJoint.Create(urdfLink.gameObject, UrdfJoint.GetJointType(joint.type), joint);
            }
            else if (joint != null)
                UrdfJoint.Create(urdfLink.gameObject, UrdfJoint.GetJointType(joint.type), joint);

        }
        
        public static Link ExportLinkData(this UrdfLink urdfLink)
        {
            if (urdfLink.transform.localScale != Vector3.one)
                Debug.LogWarning("Only visuals should be scaled. Scale on link \"" + urdfLink.gameObject.name + "\" cannot be saved to the URDF file.", urdfLink.gameObject);
            UrdfInertial urdfInertial = urdfLink.gameObject.GetComponent<UrdfInertial>();
            Link link = new Link(urdfLink.gameObject.name)
            {
                visuals = urdfLink.GetComponentInChildren<UrdfVisuals>().ExportVisualsData(),
                collisions = urdfLink.GetComponentInChildren<UrdfCollisions>().ExportCollisionsData(),
                inertial = urdfInertial == null ? null : urdfInertial.ExportInertialData()
            };
            
            return link;
        }
    }
}