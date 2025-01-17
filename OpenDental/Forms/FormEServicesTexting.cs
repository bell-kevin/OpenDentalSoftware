﻿using CodeBase;
using Microsoft.Win32;
using OpenDental.UI;
using OpenDentBusiness;
using OpenDentBusiness.Mobile;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Globalization;
using System.Data;
using System.Linq;
using System.IO;
using WebServiceSerializer;
using OpenDentBusiness.WebServiceMainHQ;
using OpenDentBusiness.WebTypes.WebSched.TimeSlot;

namespace OpenDental {
	public partial class FormEServicesSetup {		
		private Clinic _smsClinicSelected {
			get {
				if(gridSmsSummary.GetSelectedIndex()<0) {
					return new Clinic();
				}
				return ((Clinic)gridSmsSummary.ListGridRows[gridSmsSummary.GetSelectedIndex()].Tag)??new Clinic();
			}			
		}
		
		private bool IsTabValidTexting() {
			return true;
		}

		private void FillTabTexting() {
			#region Update UI
			butDefaultClinic.Visible=PrefC.HasClinicsEnabled;
			butDefaultClinicClear.Visible=PrefC.HasClinicsEnabled;
			FillGridSmsUsage();
			ShowShortCodes();
			#endregion			
		}

		private void SaveTabTexting() {

		}

		private void ShowShortCodes() {
			List<long> listClinicNums=0L.SingleItemToList();
			if(PrefC.HasClinicsEnabled) {
				listClinicNums.AddRange(Clinics.GetDeepCopy().Select(x => x.ClinicNum));
			}
			groupShortCode.Visible=listClinicNums
				.Where(x => PatComm.IsAnyShortCodeServiceEnabled(x))
				.Count()>0;
			FillShortCodes();
		}

		private void FillShortCodes() {
			bool enabled=PatComm.IsAnyShortCodeServiceEnabled(comboShortCodeClinic.SelectedClinicNum);
			labelUnsavedShortCodeChanges.Visible=false;
			checkOptInPrompt.Enabled=enabled;
			checkOptInPrompt.Checked=ClinicPrefs.GetBool(PrefName.ShortCodeOptInOnApptComplete,comboShortCodeClinic.SelectedClinicNum);
			textShortCodeOptInClinicTitle.Enabled=enabled;
			textShortCodeOptInClinicTitle.Text=GetShortCodeOptInClinicTitle();
		}

		///<summary></summary>
		private string GetShortCodeOptInClinicTitle() {			
			//Clinic 0 will be saved as a ClinicPref, not a practice wide Pref, so do not includeDefault here.
			string title=ClinicPrefs.GetPrefValue(PrefName.ShortCodeOptInClinicTitle,comboShortCodeClinic.SelectedClinicNum);
			if(!string.IsNullOrWhiteSpace(title)) {
				return title;
			}
			title=(comboShortCodeClinic.SelectedClinicNum==0) ? PrefC.GetString(PrefName.PracticeTitle) 
					: Clinics.GetDesc(comboShortCodeClinic.SelectedClinicNum);
			if(!string.IsNullOrWhiteSpace(title)) {
				return title;
			}
			//In case a clinic had an empty Description
			return PrefC.GetString(PrefName.PracticeTitle);
		}

		private void comboShortCodeClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			FillShortCodes();
		}		

		private void textShortCodeOptInClinicTitle_TextChanged(object sender,EventArgs e) {
			labelUnsavedShortCodeChanges.Visible=AreShortCodeSettingsUnsaved();
		}

		private void checkOptInPrompt_Click(object sender,EventArgs e) {
			if(!checkOptInPrompt.Checked) {
				string prompt=Lan.g(this,string.IsNullOrWhiteSpace(PrefC.GetString(PrefName.ShortCodeOptInOnApptCompleteOffScript)) 
					? "By disabling this prompt, you agree to obtain verbal confirmation from patients to send Appt Texts."
					: PrefC.GetString(PrefName.ShortCodeOptInOnApptCompleteOffScript));
				MsgBox.Show(this,prompt);
			}
			labelUnsavedShortCodeChanges.Visible=AreShortCodeSettingsUnsaved();
		}

		private bool AreShortCodeSettingsUnsaved() {
			return textShortCodeOptInClinicTitle.Text!=GetShortCodeOptInClinicTitle() 
				|| checkOptInPrompt.Checked!=ClinicPrefs.GetBool(PrefName.ShortCodeOptInOnApptComplete,comboShortCodeClinic.SelectedClinicNum);
		}

		private void butSaveShortCodes_Click(object sender,EventArgs e) {
			if(string.IsNullOrWhiteSpace(textShortCodeOptInClinicTitle.Text)) {
				string err=Lan.g(this,"Not allowed to set ")+labelShortCodeOptInClinicTitle.Text+Lan.g(this," to an empty value.");
				MessageBox.Show(err);
				return;
			}
			bool doSetInvalidClinicPrefs=false;
			bool doSetInvalidPrefs=false;
			//Only Update/Upsert if the textbox doesn't match the current setting.
			if(textShortCodeOptInClinicTitle.Text!=GetShortCodeOptInClinicTitle()) {
				if(comboShortCodeClinic.SelectedClinicNum==0) {
					doSetInvalidPrefs=Prefs.UpdateString(PrefName.ShortCodeOptInClinicTitle,textShortCodeOptInClinicTitle.Text);
				}
				else {
					doSetInvalidClinicPrefs=ClinicPrefs.Upsert(PrefName.ShortCodeOptInClinicTitle,comboShortCodeClinic.SelectedClinicNum,
						textShortCodeOptInClinicTitle.Text);
				}
			}
			if(comboShortCodeClinic.SelectedClinicNum==0) {
					doSetInvalidPrefs|=Prefs.UpdateBool(PrefName.ShortCodeOptInOnApptComplete,checkOptInPrompt.Checked);
				}
			else {
				doSetInvalidClinicPrefs|=ClinicPrefs.Upsert(PrefName.ShortCodeOptInOnApptComplete,comboShortCodeClinic.SelectedClinicNum
					,POut.Bool(checkOptInPrompt.Checked));
			}
			if(doSetInvalidPrefs) {
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			if(doSetInvalidClinicPrefs) {
				DataValid.SetInvalid(InvalidType.ClinicPrefs);
			}
			FillShortCodes();
		}

		private void AuthorizeTabTexting(bool allowEdit) {
			butDefaultClinic.Enabled=allowEdit;
			butDefaultClinicClear.Enabled=allowEdit;
		}

		private void butDefaultClinic_Click(object sender,EventArgs e) {
			if(_smsClinicSelected.ClinicNum==0) {
				MsgBox.Show(this,"Select clinic to make default.");
				return;
			}
			Prefs.UpdateLong(PrefName.TextingDefaultClinicNum,(_smsClinicSelected.ClinicNum));
			Signalods.SetInvalid(InvalidType.Prefs);
			FillGridSmsUsage();
		}

		private void butDefaultClinicClear_Click(object sender,EventArgs e) {
			Prefs.UpdateLong(PrefName.TextingDefaultClinicNum,0);
			Signalods.SetInvalid(InvalidType.Prefs);
			FillGridSmsUsage();
		}

		private void FillGridSmsUsage() {
			List<Clinic> listClinics=Clinics.GetForUserod(Security.CurUser);
			if(!PrefC.HasClinicsEnabled) { //No clinics so just get the practice as a clinic.
				listClinics.Clear();
				listClinics.Add(Clinics.GetPracticeAsClinicZero());
			}
			var items=SmsPhones.GetSmsUsageLocal(listClinics.Select(x => x.ClinicNum).ToList(),dateTimePickerSms.Value,
				WebServiceMainHQProxy.EServiceSetup.SignupOut.SignupOutPhone.ToSmsPhones(_signupOut.Phones))
				.Rows.Cast<DataRow>().Select(x => new {
					ClinicNum=PIn.Long(x["ClinicNum"].ToString()),
					PhoneNumber=x["PhoneNumber"].ToString(),
					CountryCode=x["CountryCode"].ToString(),
					SentMonth=PIn.Int(x["SentMonth"].ToString()),
					SentCharge=PIn.Double(x["SentCharge"].ToString()),
					SentDiscount=PIn.Double(x["SentDiscount"].ToString()),
					SentPreDiscount=PIn.Double(x["SentPreDiscount"].ToString()),
					RcvMonth=PIn.Int(x["ReceivedMonth"].ToString()),
					RcvCharge=PIn.Double(x["ReceivedCharge"].ToString())
			});
			bool doShowDiscount=items.Any(x => x.SentDiscount.IsGreaterThan(0));
			gridSmsSummary.BeginUpdate();
			gridSmsSummary.ListGridColumns.Clear();
			if(PrefC.HasClinicsEnabled) {
				gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Default"),80) { TextAlign=HorizontalAlignment.Center });
			}
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Location"),170,HorizontalAlignment.Left));
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Subscribed"),80,HorizontalAlignment.Center));
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Primary\r\nPhone Number"),105,HorizontalAlignment.Center));
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Country\r\nCode"),60,HorizontalAlignment.Center));
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Limit"),80,HorizontalAlignment.Right));
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Sent\r\nFor Month"),70,HorizontalAlignment.Right));
			if(doShowDiscount) {
				gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Sent\r\nPre-Discount"),80,HorizontalAlignment.Right));
				gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Sent\r\nDiscount"),70,HorizontalAlignment.Right));
			}
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Sent\r\nCharges"),70,HorizontalAlignment.Right));
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Received\r\nFor Month"),70,HorizontalAlignment.Right));
			gridSmsSummary.ListGridColumns.Add(new GridColumn(Lan.g(this,"Received\r\nCharges"),70,HorizontalAlignment.Right));
			gridSmsSummary.ListGridRows.Clear();
			foreach(Clinic clinic in listClinics) {
				GridRow row=new GridRow();
				if(PrefC.HasClinicsEnabled) { //Default texting clinic?
					row.Cells.Add(clinic.ClinicNum==PrefC.GetLong(PrefName.TextingDefaultClinicNum) ? "X" : "");
				}
				row.Cells.Add(clinic.Abbr); //Location.				
				var dataRow=items.FirstOrDefault(x => x.ClinicNum==clinic.ClinicNum);				
				if(dataRow==null) {
					row.Cells.Add("No");//subscribed
					row.Cells.Add("");//phone number
					row.Cells.Add("");//country code
					row.Cells.Add((0f).ToString("c",new CultureInfo("en-US")));//montly limit
					row.Cells.Add("0");//Sent Month
					if(doShowDiscount) {
						row.Cells.Add((0f).ToString("c",new CultureInfo("en-US")));//Sent Pre-Discount
						row.Cells.Add((0f).ToString("c",new CultureInfo("en-US")));//Sent Discount
					}
					row.Cells.Add((0f).ToString("c",new CultureInfo("en-US")));//Sent Charge
					row.Cells.Add("0");//Rcvd Month
					row.Cells.Add((0f).ToString("c",new CultureInfo("en-US")));//Rcvd Charge
				}
				else {
					row.Cells.Add(clinic.SmsContractDate.Year>1800 ? Lan.g(this,"Yes") : Lan.g(this,"No"));
					row.Cells.Add(dataRow.PhoneNumber);
					row.Cells.Add(dataRow.CountryCode);
					row.Cells.Add(clinic.SmsMonthlyLimit.ToString("c",new CultureInfo("en-US")));//Charge this month (Must always be in USD)
					row.Cells.Add(dataRow.SentMonth.ToString());
					if(doShowDiscount) {
						row.Cells.Add(dataRow.SentPreDiscount.ToString("c",new CultureInfo("en-US")));
						row.Cells.Add(dataRow.SentDiscount.ToString("c",new CultureInfo("en-US")));
					}
					row.Cells.Add(dataRow.SentCharge.ToString("c",new CultureInfo("en-US")));
					row.Cells.Add(dataRow.RcvMonth.ToString());
					row.Cells.Add(dataRow.RcvCharge.ToString("c",new CultureInfo("en-US")));
				}
				row.Tag=clinic;
				gridSmsSummary.ListGridRows.Add(row);
			}
			if(listClinics.Count>1) {//Total row if there is more than one clinic (Will not display for practice because practice will have no clinics.
				GridRow row=new GridRow();
				row.Cells.Add("");
				row.Cells.Add("");
				row.Cells.Add("");
				row.Cells.Add("");
				row.Cells.Add(Lans.g(this,"Total"));
				row.Cells.Add(listClinics.Where(x => items.Any(y => y.ClinicNum==x.ClinicNum)).Sum(x => x.SmsMonthlyLimit).ToString("c",new CultureInfo("en-US")));
				row.Cells.Add(items.Sum(x => x.SentMonth).ToString());
				if(doShowDiscount) {
					row.Cells.Add(items.Sum(x => x.SentPreDiscount).ToString("c",new CultureInfo("en-US")));
					row.Cells.Add(items.Sum(x => x.SentDiscount).ToString("c",new CultureInfo("en-US")));
				}
				row.Cells.Add(items.Sum(x => x.SentCharge).ToString("c",new CultureInfo("en-US")));
				row.Cells.Add(items.Sum(x => x.RcvMonth).ToString());
				row.Cells.Add(items.Sum(x => x.RcvCharge).ToString("c",new CultureInfo("en-US")));
				row.ColorBackG=Color.LightYellow;
				gridSmsSummary.ListGridRows.Add(row);
			}
			gridSmsSummary.EndUpdate();
		}

		private void butBackMonth_Click(object sender,EventArgs e) {
			dateTimePickerSms.Value=dateTimePickerSms.Value.AddMonths(-1);
		}

		private void butFwdMonth_Click(object sender,EventArgs e) {
			dateTimePickerSms.Value=dateTimePickerSms.Value.AddMonths(1);//triggers refresh
		}

		private void butThisMonth_Click(object sender,EventArgs e) {
			dateTimePickerSms.Value=DateTime.Now.Date;//triggers refresh
		}

		private void dateTimePickerSms_ValueChanged(object sender,EventArgs e) {
			FillGridSmsUsage();
		}
	}
}
