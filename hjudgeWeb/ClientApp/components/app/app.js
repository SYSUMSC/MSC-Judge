﻿import navmenu from '../navmenu/navmenu.vue';
import { Get } from '../../utilities/requestHelper';

export default {
    data: () => ({
        userInfo: {},
        themeIcon: 'brightness_4',
        darkTheme: false,
        msgTitle: '',
        msgContent: '',
        msgUp: false,
        snackContent: '',
        snackUp: false,
        snackColor: '',
        snackTimeout: 5000
    }),
    components: {
        navmenu: navmenu
    },
    mounted() {
        let theme = localStorage.getItem('theme');
        if (theme && theme === '2') {
            this.themeIcon = 'brightness_5';
            this.darkTheme = true;
        }
        this.getUserInfo();
    },
    methods: {
        switchTheme: function () {
            if (!this.darkTheme) {
                this.themeIcon = 'brightness_5';
                this.darkTheme = true;
                localStorage.setItem('theme', '2');
            }
            else {
                this.themeIcon = 'brightness_4';
                this.darkTheme = false;
                localStorage.setItem('theme', '1');
            }
        },
        getUserInfo: function () {
            Get('/Account/GetUserInfo')
                .then(res => res.json())
                .then(data => {
                    this.userInfo = data;
                    if (this.userInfo && this.userInfo.coinsBonus !== 0) {
                        this.showMsg('每日登录奖励', `获得 ${this.userInfo.coinsBonus} 金币。连续登录将获得更多金币呦~`);
                    }
                })
                .catch(() => alert('网络错误'));
        },
        updateFortune: function () {
            Get('/Account/GetFortune')
                .then(res => res.json())
                .then(data => {
                    if (this.userInfo && data) {
                        this.userInfo.coins = data.coins;
                        this.userInfo.experience = data.experience;
                    }
                })
                .catch(() => {
                    //ignore
                });
        },
        showMsg: function (title, content) {
            this.msgTitle = title;
            this.msgContent = content;
            this.msgUp = true;
        },
        showSnack: function (content, color, timeout) {
            this.snackContent = content;
            this.snackColor = color;
            this.snackTimeout = timeout;
            this.snackUp = true;
        }
    }
};