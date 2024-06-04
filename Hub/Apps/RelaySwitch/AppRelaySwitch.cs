﻿using System.Collections.Generic;

using HomeOS.Hub.Common;
using HomeOS.Hub.Platform.Views;
using System;


namespace HomeOS.Hub.Apps.RelaySwitch
{
    [System.AddIn.AddIn("HomeOS.Hub.Apps.RelaySwitch")]
    public class AppRelaySwitch : ModuleBase
    {
        AppRelaySwitchService service;
        SafeServiceHost serviceHost;
        WebFileServer webUiServer;

        Dictionary<VPort, VCapability> registeredSensors = new Dictionary<VPort, VCapability>();
        Dictionary<VPort, VCapability> registeredActuators = new Dictionary<VPort, VCapability>();

        public int IsOn = 0;

        public override void Start()
        {
            logger.Log("Started: {0}", ToString());

            service = new AppRelaySwitchService(this, logger);

            serviceHost = AppRelaySwitchService.CreateServiceHost(logger, this, service, moduleInfo.BaseURL() + "/webapp");

            serviceHost.Open();

            webUiServer = new WebFileServer(moduleInfo.BinaryDir(), moduleInfo.BaseURL(), logger);

            logger.Log("{0}: service is open for business at {1}", ToString(), moduleInfo.BaseURL());

            //... get the list of current ports from the platform
            IList<VPort> allPortsList = GetAllPortsFromPlatform();

            if (allPortsList != null)
            {
                foreach (VPort port in allPortsList)
                {
                    PortRegistered(port);
                }
            }
        }

        public override void Stop()
        {
            serviceHost.Abort();
        }

        public override void PortRegistered(VPort port)
        {
            lock (this)
            {
                if (Role.ContainsRole(port, RoleSensor.RoleName))
                {
                    VCapability capability = GetCapability(port, Constants.UserSystem);

                    if (registeredSensors.ContainsKey(port))
                        registeredSensors[port] = capability;
                    else
                        registeredSensors.Add(port, capability);

                    if (capability != null)
                    {
                        port.Subscribe(RoleSensor.RoleName, RoleSensor.OpGetName,
                               this.ControlPort, capability, this.ControlPortCapability);
                    }
                }
                if (Role.ContainsRole(port, RoleActuator.RoleName))
                {
                    VCapability capability = GetCapability(port, Constants.UserSystem);

                    if (registeredActuators.ContainsKey(port))
                        registeredActuators[port] = capability;
                    else
                        registeredActuators.Add(port, capability);

                    if (capability != null)
                    {
                        port.Subscribe(RoleActuator.RoleName, RoleActuator.OpPutName, this.ControlPort, capability, this.ControlPortCapability);
                    }
                }
            }
        }

        public void SetRelaySwitch(string amount)
        {
            int amountCnt = Int32.Parse(amount);
            foreach (var port in registeredActuators.Keys)
            {
                if (registeredActuators[port] == null)
                    registeredActuators[port] = GetCapability(port, Constants.UserSystem);

                if (registeredActuators[port] != null)
                {
                    IList<VParamType> parameters = new List<VParamType>();
                    parameters.Add(new ParamType((int)amountCnt));

                    port.Invoke(RoleActuator.RoleName, RoleActuator.OpPutName, parameters, ControlPort, registeredActuators[port], ControlPortCapability);
                }
            }
        }

        public override void PortDeregistered(VPort port)
        {
            lock (this)
            {
                if (Role.ContainsRole(port, RoleSensor.RoleName))
                {
                    if (registeredSensors.ContainsKey(port))
                    {
                        registeredSensors.Remove(port);
                        logger.Log("{0} removed sensor port {1}", this.ToString(), port.ToString());
                    }
                }
                if (Role.ContainsRole(port, RoleActuator.RoleName))
                {
                    if (registeredActuators.ContainsKey(port))
                    {
                        registeredActuators.Remove(port);
                        logger.Log("{0} removed actuator port {1}", this.ToString(), port.ToString());
                    }
                }
            }
        }

        public override void OnNotification(string roleName, string opName, IList<VParamType> retVals, VPort senderPort)
        {
            logger.Log("Notitification from {0} for {0}", roleName, opName);
            if (retVals.Count >= 1)
            {
                this.IsOn = (int)retVals[0].Value();
            }
            else
            {
                logger.Log("{0}: got unexpected retvals [{1}] from {2}", ToString(), retVals.Count.ToString(), senderPort.ToString());
            }
        }
    }

}
