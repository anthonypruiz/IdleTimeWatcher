# IdleTimeWatcher - (If you see this text I'm currently setting up repository)
> This program gives the ability to monitor the network by Idletime with Zabbix and Graphana
## Features
* Visual user activity monitoring (IdleTime) in real time when workstation is in use. 
* If there's no user activity, you'll see how long they've been away by a time counter in seconds.
* Email Alerting when users log in and log out of their workstations.
## Background
This project originally was a solution to address attendence. Some employees did not clock-in or clock-out and the office management wanted to find a way to know when people signed into their computers and when they left for the day. Not only that they also wanted to get email alerts when these employees logged in and out of their PCs. The users all have Windows 7 transitioning to Windows 10 PCs, I had built a LAMP server already and had Zabbix with Graphana servers monitoring the network already. So after a few other methods that didn't work I ended up with a workable solution.
<br>
Pinvoking GetLastInput we're able to extract the value of the last time there was interaction with the computer wheather by keyboard or mouse movement. So after this value is converted to seconds I set an infinite loop with a sleep interval between 2 and 10 seconds to send this value to Zabbix with the Zabbix_Sender. I had Zabbix already connected to Grafana so I created a page template utilizing the singlestat panel for the IdleTime value, added the cool sparkline graphic and show the value of seconds counting up per host. So in an unintended fashion I can now see when people are at their computers and how long they've been away. To finish off the solution I set a trigger and created an action in Zabbix to notify the interested parties in management when "nodata" is logged in Zabbix Idletime over 5 minutes. So after a user logs out the trigger is set and management gets an email saying so and so has logged out. When they log in the next day the problem is resolved and Zabbix sends out an email saying so and so has logged in. Not only does this work here but the Single Stat panel associated with the workstation glows red in grafana page when the trigger is set until they log back in and it's resolveed.
## Requirements
Before installing the program on the host station you'll need these prerequisites.
* LAMP Server
* Zabbix Server installed on the LAMP
* Grafana Server installed on the LAMP
## Installation Instructions
