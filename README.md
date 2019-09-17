# IdleTimeWatcher - (If you see this text I'm currently setting up repository)
> This program gives the ability to monitor the network by Idletime with Zabbix and Graphana
## Features
* Visual user activity monitoring (IdleTime) in real time when workstation is in use. 
* If there's no user activity, you'll see how long they've been away by a time counter in seconds.
* Email Alerting when users log in and log out of their workstations.
* *In the future I'll refine the program to send notifications without the need for Zabbix and Grafana.*
---
![](https://anthonypaulruiz.com/wp-content/uploads/2019/09/grafanaIdleTime3.png)
> <p align="center">A panel showing the last user interaction by keyboard or mouse.</p>
---
## Background
This project originally was a solution to address attendence. Some employees did not clock-in or clock-out and the office management wanted to find a way to know when people signed into their computers and when they left for the day. Not only that they also wanted to get email alerts when these employees logged in and out of their PCs. The users all have Windows 7 transitioning to Windows 10 PCs, I had built a LAMP server already and had Zabbix with Graphana servers monitoring the network already. So after a few other methods that didn't work I ended up with a workable solution.
<br>
Pinvoking GetLastInput we're able to extract the value of the last time there was interaction with the computer wheather by keyboard or mouse movement. So after this value is converted to seconds I set an infinite loop with a sleep interval between 2 and 10 seconds to send this value to Zabbix with the Zabbix_Sender. I had Zabbix already connected to Grafana so I created a page template utilizing the singlestat panel for the IdleTime value, added the cool sparkline graphic and show the value of seconds counting up per host. So in an unintended fashion I can now see when people are at their computers and how long they've been away. To finish off the solution I set a trigger and created an action in Zabbix to notify the interested parties in management when "nodata" is logged in Zabbix Idletime over 5 minutes. So after a user logs out the trigger is set and management gets an email saying so and so has logged out. When they log in the next day the problem is resolved and Zabbix sends out an email saying so and so has logged in. Not only does this work here but the Single Stat panel associated with the workstation glows red in grafana page when the trigger is set until they log back in and it's resolved.
---
## Requirements
Before installing the program on the host station you'll need these prerequisites.
* LAMP Server
* Zabbix Server installed on the LAMP
* Grafana Server installed on the LAMP
---
## Installation Instructions
1. Create a new item for the host group in your network in Zabbix, name it whatever but be sure the "key" name is identical to what's set in the program which by default is "idletime", set the units to "s" for seconds
---
![](https://anthonypaulruiz.com/wp-content/uploads/2019/09/zabbixDone.png)

2. copy all the contents of the /IdleTime/bin/Debug/ into a folder named "IdleTime" @ C:\IdleTime\
> The program won't work if run as a service because windows service doesn't operate on the same dimension(IDK what it's called someone correct me), so in order for the program to work we need it to run as a scheduled task. (I will add onto this program in the future an automatic installer for the scheduled task but for now here's the manual steps)
## **Create "Scheduled Task"**
3.
* Name it **"IdleTime"**
* "When running the task, use the following user account:" **UserName**
* **Run only when user is logged on.**
### Triggers
* Begin the task **At logon**
* Repeat task every **5 minutes** for a duration of **Indefinitely**
### Actions
* Start a program
* Browse to the **idletime.exe location**
### Conditions
* Check on the Network tab to **run only if connected to the network.**
### Settings
* If the task fails, restart every **1 minute**
* **Uncheck the checkbox** "Stop the task if it runs longer than"
* if the task is already running, then the follwing rule applies: **Do not start a new instance**
---
> Now the task can run and you'll recieve data on the host computer for idletime.
## Optional - Grafana Panel Setup & Email Alerting
### Email Alerting for Attendence notifications
1. Create a trigger in Zabbix [creating a trigger in Zabbix](https://www.zabbix.com/documentation/4.2/manual/config/triggers/trigger)
* I set my trigger to {MyHostname:idletime.nodata(5m)}=1 **(no data received for 5 minutes)**
2. Create an Action where you'll send an email to whomever you want too. 
* I modified the Alert message to simply say '{HOST.NAME} Has Logged out.'
* Recovery options send an email with an Alert message '{HOST.NAME} Has Logged In.'
> Modify this in any way you see fit, you could set triggers if someone is away from their computer for X amount of time etc. So now, after the person logs out the trigger will send an email alert that someone has logged out, and when they log in you'll get an email that they are logged in. If you follow the steps for Grafana there are really neat visuals I'll define how below.
### Grafana panel setup
![](https://anthonypaulruiz.com/wp-content/uploads/2019/09/dashboard-2.png)
![](https://anthonypaulruiz.com/wp-content/uploads/2019/09/dashboard2.png)
