﻿/*
 *  Dover Framework - OpenSource Development framework for SAP Business One
 *  Copyright (C) 2014  Eduardo Piva
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *  
 *  Contact me at <efpiva@gmail.com>
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.Core.Logging;
using System.ServiceModel;
using Dover.Framework.Monad;

namespace Dover.Framework.Service
{
    public class AppEventHandler
    {
        private MicroBoot microBoot;
        private I18NService i18nManager;
        public ILogger Logger { get; set; }

        public AppEventHandler(MicroBoot microBoot, I18NService i18nManager)
        {
            this.microBoot = microBoot;
            this.i18nManager = i18nManager;
        }

        internal void sapApp_AppEvent(SAPbouiCOM.BoAppEventTypes EventType)
        {
            try
            {
                switch (EventType)
                {
                    case SAPbouiCOM.BoAppEventTypes.aet_ServerTerminition:
                        Logger.Info(Messages.Shutdown);
                        microBoot.InceptionAddinManager.Do(x => x.ShutdownAddins());
                        AppDomain.Unload(microBoot.Inception);
                        System.Windows.Forms.Application.Exit();
                        break;
                    case SAPbouiCOM.BoAppEventTypes.aet_LanguageChanged:
                        try
                        {
                            ConfigureI18N();
                        }
                        catch (Exception er)
                        {
                            Logger.Error(Messages.EventLanguageChangedError, er);
                        }
                        break;
                    case SAPbouiCOM.BoAppEventTypes.aet_CompanyChanged:
                        try
                        {
                            Reboot();
                        }
                        catch (Exception er)
                        {
                            Logger.Error(Messages.EventCompanyChangedError, er);
                        }
                        break;
                    case SAPbouiCOM.BoAppEventTypes.aet_ShutDown:
                        Logger.Info(Messages.Shutdown);
                        microBoot.InceptionAddinManager.Do(x => x.ShutdownAddins());
                        AppDomain.Unload(microBoot.Inception);
                        System.Windows.Forms.Application.Exit();
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.Message, e);
                Environment.Exit(20);
            }
        }

        private void ConfigureI18N()
        {
            microBoot.InceptionAddinManager.ConfigureAddinsI18N();
            i18nManager.ConfigureThreadI18n(System.Threading.Thread.CurrentThread);
        }

        private void Reboot()
        {
            try
            {
                Logger.Info(String.Format(Messages.Restarting, this.GetType().Assembly.GetName().Version));
                Logger.Info(Messages.Shutdown);
                microBoot.InceptionAddinManager.Do(x => x.ShutdownAddins());
                AppDomain.Unload(microBoot.Inception);
                microBoot.StartInception();
                microBoot.Boot();
            }
            catch (Exception e)
            {
                Logger.Fatal(Messages.ErrorReboot, e);
                Environment.Exit(10);
            }
        }
    }
}
