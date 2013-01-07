delimiter $$

CREATE DATABASE `bnet` /*!40100 DEFAULT CHARACTER SET utf8 */$$

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
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8$$


CREATE TABLE `dayz_online` (
  `dayz_server_id` int(11) unsigned NOT NULL,
  `slot` tinyint(3) unsigned NOT NULL,
  `guid` varchar(32) NOT NULL,
  `name` varchar(64) NOT NULL,
  `ip_address` varchar(45) NOT NULL,
  `lobby` tinyint(3) unsigned NOT NULL,
  `ping` int(11) NOT NULL,
  `verified` tinyint(4) NOT NULL,
  `first_seen` datetime NOT NULL,
  `last_seen` datetime NOT NULL,
  `online` tinyint(4) NOT NULL,
  PRIMARY KEY (`dayz_server_id`,`first_seen`,`guid`),
  KEY `FK_dayzonline_dayzserver_idx` (`dayz_server_id`),
  KEY `guid_online` (`online`,`guid`),
  CONSTRAINT `FK_dayzonline_dayzserver` FOREIGN KEY (`dayz_server_id`) REFERENCES `dayz_server` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8$$

