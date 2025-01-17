﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenDentBusiness;

namespace UnitTestsCore {
	public class SmsToMobileT {

		public static SmsToMobile CreateSmsToMobile(Patient pat,string guidMessage,SmsMessageSource source,long clinicNum=0) {
			string guidBatch=Guid.NewGuid().ToString();
			SmsToMobile smsToMobile=new SmsToMobile() {
				GuidBatch=guidBatch,
				GuidMessage=guidMessage,
				MsgType=source,
				PatNum=pat.PatNum,
				ClinicNum=clinicNum,
				MobilePhoneNumber=pat.WirelessPhone
			};
			smsToMobile.SmsToMobileNum=SmsToMobiles.Insert(smsToMobile);
			return smsToMobile;
		}
	}
}
