﻿import navmenu from '../navmenu/navmenu.vue';
import { Get } from '../../utilities/requestHelper';

export default {
    data: () => ({
        userInfo: {},
        themeIcon: 'brightness_4',
        darkTheme: false
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
        Get('/Account/GetUserInfo')
            .then(res => res.json())
            .then(data => this.userInfo = data)
            .catch(() => alert('网络错误'));
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
        }
    }
};