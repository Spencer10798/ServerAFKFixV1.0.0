/**
* <author>Christophe Roblin</author>
* <email>lifxmod@gmail.com</email>
* <url>lifxmod.com</url>
* <credits></credits>
* <description>Disconnects user on preConnect if server is full</description>
* <license>GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007</license>
*
* Modified by Spencer10798 - Github.com/Spencer10798
* January 17th, 2024
* Repurposed to serve as an automatic AFK kicker.
*/

if (!isObject(LiFxServerAFKFix))
{
    new ScriptObject(LiFxServerAFKFix)
    {
    };
}
if(!isObject($LiFx::FullServerFixIdleTimeout))
  $LiFx::FullServerFixIdleTimeout = 60;

package LiFxServerAFKFix
{
  function LiFxServerAFKFix::setup() {
    LiFx::registerCallback($LiFx::hooks::onPostConnectRoutineCallbacks, onPostConnectRequest, LiFxServerAFKFix);
    LiFx::registerCallback($LiFx::hooks::onInitServerDBChangesCallbacks, dbInit, LiFxServerAFKFix);
    LiFx::registerCallback($LiFx::hooks::onConnectCallbacks,onConnectClient, LiFxServerAFKFix);
  }
  
  function LiFxServerAFKFix::dbInit() {
    dbi.Update("ALTER TABLE `character` ADD COLUMN `LastUpdated` TIMESTAMP NULL DEFAULT NULL AFTER `DeleteTimestamp`");
    dbi.Update("DROP TRIGGER IF EXISTS `character_before_update`;");
    %character_before_update = "CREATE TRIGGER `character_before_update` BEFORE UPDATE ON `character`; FOR EACH ROW BEGIN\n";
    %character_before_update = %character_before_update @ "IF(NEW.GeoID != OLD.GeoID OR NEW.GeoAlt != OLD.GeoAlt) THEN\n";
    %character_before_update = %character_before_update @ "SET NEW.LastUpdated = CURRENT_TIMESTAMP;\n";
    %character_before_update = %character_before_update @ "END IF;\n";
    %character_before_update = %character_before_update @ "END\n";
    dbi.Update(%character_before_update);
  }
  function LiFxServerAFKFix::version() {
    return "v1.0.0.AFK";
  }

  function LiFxServerAFKFix::onConnectClient(%this, %client) {
    dbi.Update("UPDATE `character` SET LastUpdated = now() WHERE id=" @ %client.getCharacterId());
  }
  function LiFxServerAFKFix::onPostConnectRequest(%this, %client, %nettAddress, %name) {
    %client.ConnectedTime = getUnixTime();
    if ($Server::PlayerCount > $Server::MaxPlayers)
    {
        LiFxServerAFKFix.ConReq = new ScriptObject() {
          Client = %client;
          NettAddress = %nettAddress;
          Name = %name;
        };
        %client.ConnectedTime = getUnixTime();
        dbi.Select(LiFxServerAFKFix, "AFKKick", "SELECT c.ID AS ClientId FROM `lifx_character` lc LEFT JOIN `character` c ON c.ID = lc.id CROSS JOIN nyu_ttmod_info info WHERE TIMESTAMPDIFF(MINUTE,c.LastUpdated,CURRENT_TIMESTAMP) > " @ $LiFx::FullServerFixIdleTimeout @ " AND (lc.loggedIn > info.boot_time) AND (lc.loggedOut < lc.loggedIn) ORDER BY c.lastUpdated ASC LIMIT 1");
    }
    dbi.Update("UPDATE `character` SET LastUpdated = now() WHERE AccountID=" @ %client.getAccountId());
  }

  function LiFxServerAFKFix::AFKKick(%this,%rs) {
    if(%rs.ok() && %rs.nextRecord())
    {
      %ClientID = %rs.getFieldValue("ClientID");

      for(%id = 0; %id < ClientGroup.getCount(); %id++)
      {
        %client = ClientGroup.getObject(%id);
        if(%ClientID == %client.getCharacterId())
        {
          warn("Character " SPC %client SPC " kicked for idling");
          %client.scheduleDelete("You have been ejected from the server due to inactivity (AFK)", 100);
          break;
        } 
      }
      if(ClientGroup.getCount() == %id) {
        warn("Connection from" SPC %this.ConReq.NetAddress SPC "(" @ %this.ConReq.Name @ ")" SPC "dropped due to CR_SERVERFULL_NO_IDLERS");
        %this.ConReq.Client.scheduleDelete("Server is full without idlers, try again in 5 mins", 100);
      }
      
    }
    else {   
      warn("Connection from" SPC %this.ConReq.NetAddress SPC "(" @ %this.ConReq.Name @ ")" SPC "dropped due to CR_SERVERFULL");
      %this.ConReq.Client.scheduleDelete("Server is full", 100);
    }
    dbi.remove(%rs);
    %rs.delete();
  }
};
activatePackage(LiFxServerAFKFix);
LiFx::registerCallback($LiFx::hooks::mods, setup, LiFxServerAFKFix);