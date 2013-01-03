delimiter $$

CREATE DATABASE `bnet` /*!40100 DEFAULT CHARACTER SET latin1 */$$
USE `bnet` $$


CREATE TABLE `dayz_online` (
  `dayz_server_id` int(11) unsigned NOT NULL,
  `slot` tinyint(3) unsigned NOT NULL,
  `name` varchar(300) NOT NULL,
  `ip_address` varchar(45) NOT NULL,
  `guid` varchar(32) NOT NULL,
  `lobby` tinyint(3) unsigned NOT NULL,
  `ping` int(11) NOT NULL,
  `verified` tinyint(4) NOT NULL,
  PRIMARY KEY (`dayz_server_id`,`slot`),
  UNIQUE KEY `name_UNIQUE` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1$$



CREATE TABLE `dayz_server` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `server_id` int(11) unsigned NOT NULL,
  `short_name` varchar(16) NOT NULL,
  `name` varchar(400) NOT NULL,
  `db_type` int(11) unsigned NOT NULL,
  `db_host` varchar(50) NOT NULL,
  `db_port` int(11) NOT NULL,
  `db_dbname` varchar(50) NOT NULL,
  `db_user` varchar(50) NOT NULL,
  `db_password` varchar(50) NOT NULL,
  `rcon_host` varchar(50) NOT NULL,
  `rcon_port` int(11) NOT NULL,
  `rcon_pwd` varchar(50) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id_UNIQUE` (`id`),
  KEY `dcvfs_idx` (`server_id`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=latin1$$



CREATE DEFINER=`root`@`localhost` PROCEDURE `dayz_clear_online`(in dayz_srv_id int)
BEGIN

	DELETE FROM dayz_online 
    WHERE dayz_online.dayz_server_id = dayz_srv_id;

END$$

