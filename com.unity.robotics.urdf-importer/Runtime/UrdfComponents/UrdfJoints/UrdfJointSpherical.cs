/*
© Siemens AG, 2018-2019
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
    public class UrdfJointSpherical : UrdfJoint
    {
        public override JointTypes JointType => JointTypes.Spherical;

        public static UrdfJoint Create(GameObject linkObject)
        {
            UrdfJointSpherical urdfJoint = linkObject.AddComponent<UrdfJointSpherical>();
#if UNITY_2020_1_OR_NEWER
            urdfJoint.unityJoint = linkObject.GetComponent<ArticulationBody>();
            urdfJoint.unityJoint.jointType = ArticulationJointType.SphericalJoint;

#else
            urdfJoint.unityJoint = linkObject.AddComponent<HingeJoint>();
            urdfJoint.unityJoint.autoConfigureConnectedAnchor = true;
#endif

            return urdfJoint;
        }

        #region Runtime

        /// <summary>
        /// Returns the current position of the joint in radians
        /// </summary>
        /// <returns>floating point number for joint position in radians</returns>
        public override float GetPosition()
        {
#if UNITY_2020_1_OR_NEWER
            ArticulationReducedSpace jointPos = ((ArticulationBody)unityJoint).jointPosition;
            float totalPos = 0;
            for (int i = 0; i < jointPos.dofCount; i++)
            {
                totalPos += jointPos[i] * jointPos[i];
            }
            return Mathf.Sqrt(totalPos);
#else
            return -((HingeJoint)unityJoint).angle * Mathf.Deg2Rad;
#endif
        }

        /// <summary>
        /// Returns the current velocity of joint in radians per second
        /// </summary>
        /// <returns>floating point for joint velocity in radians per second</returns>
        public override float GetVelocity()
        {
#if UNITY_2020_1_OR_NEWER
            ArticulationReducedSpace jointVel = ((ArticulationBody)unityJoint).jointVelocity;
            float totalVel = 0;
            for (int i = 0; i < jointVel.dofCount; i++)
            {
                totalVel += jointVel[i] * jointVel[i];
            }
            return Mathf.Sqrt(totalVel);
#else
            return -((HingeJoint)unityJoint).velocity * Mathf.Deg2Rad;
#endif
        }

        /// <summary>
        /// Returns current joint torque in Nm
        /// </summary>
        /// <returns>floating point in Nm</returns>
        public override float GetEffort()
        {
#if UNITY_2020_1_OR_NEWER
            ArticulationReducedSpace jointForce = unityJoint.jointForce;
            float totalForce = 0;
            for (int i = 0; i < jointForce.dofCount; i++)
            {
                totalForce += jointForce[i] * jointForce[i];
            }
            return Mathf.Sqrt(totalForce);
#else
            return -((HingeJoint)unityJoint).motor.force;
#endif
        }


        /// <summary>
        /// Rotates the joint by deltaState radians 
        /// </summary>
        /// <param name="deltaState">amount in radians by which joint needs to be rotated</param>
        protected override void OnUpdateJointState(float deltaState)
        {
#if UNITY_2020_1_OR_NEWER
            ArticulationDrive xdrive = unityJoint.xDrive;
            ArticulationDrive ydrive = unityJoint.yDrive;
            ArticulationDrive zdrive = unityJoint.zDrive;
            
            float distributedDelta = deltaState / 3.0f;
            xdrive.target += distributedDelta * Mathf.Rad2Deg;
            ydrive.target += distributedDelta * Mathf.Rad2Deg;
            zdrive.target += distributedDelta * Mathf.Rad2Deg;
            
            unityJoint.xDrive = xdrive;
            unityJoint.yDrive = ydrive;
            unityJoint.zDrive = zdrive;
#else
            Quaternion rot = Quaternion.AngleAxis(-deltaState * Mathf.Rad2Deg, unityJoint.axis);
            transform.rotation = transform.rotation * rot;
#endif
        }

        #endregion

        protected override void ImportJointData(Joint joint)
        {
            AdjustMovement(joint);
            SetDynamics(joint.dynamics);
        }

        protected override Joint ExportSpecificJointData(Joint joint)
        {
#if UNITY_2020_1_OR_NEWER
            joint.dynamics = new Joint.Dynamics(unityJoint.angularDamping, unityJoint.jointFriction);
#else
            joint.dynamics = new Joint.Dynamics(((HingeJoint)unityJoint).spring.damper, ((HingeJoint)unityJoint).spring.spring);
#endif

            return joint;
        }

        /// <summary>
        /// Reads axis joint information and rotation to the articulation body to produce the required motion
        /// </summary>
        /// <param name="joint">Structure containing joint information</param>
        protected override void AdjustMovement(Joint joint)
        {
#if UNITY_2020_1_OR_NEWER
            unityJoint.linearLockX = ArticulationDofLock.LockedMotion;
            unityJoint.linearLockY = ArticulationDofLock.LockedMotion;
            unityJoint.linearLockZ = ArticulationDofLock.LockedMotion;
            unityJoint.swingYLock = ArticulationDofLock.FreeMotion;
            unityJoint.swingZLock = ArticulationDofLock.FreeMotion;
            unityJoint.twistLock = ArticulationDofLock.FreeMotion;
#endif
        }
    }
}

