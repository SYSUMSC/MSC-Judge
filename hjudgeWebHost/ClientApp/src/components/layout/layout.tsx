import * as React from "react";
import { NavLink } from 'react-router-dom';
import { Container, Menu, Icon, TransitionablePortal, Segment, Header, SemanticCOLORS, Dropdown, Divider } from 'semantic-ui-react';
import { UserInfo } from "../../interfaces/userInfo";
import Login from "../account/login";
import Register from "../account/register";
import { Post } from "../../utils/requestHelper";
import { ResultModel } from "../../interfaces/resultModel";

interface PortalState {
  open: boolean,
  header: string,
  message: string,
  color: SemanticCOLORS
}

interface LayoutState {
  portal: PortalState
  loginModalOpen: boolean,
  registerModalOpen: boolean
}

interface LayoutProps {
  userInfo: UserInfo,
  refreshUserInfo: (() => void)
}

export default class Layout extends React.Component<LayoutProps, LayoutState> {
  constructor(props: any) {
    super(props);
    this.state = {
      portal: {
        open: false,
        header: '',
        message: '',
        color: 'black'
      },
      loginModalOpen: false,
      registerModalOpen: false
    }
    this.login = this.login.bind(this);
    this.logout = this.logout.bind(this);
    this.register = this.register.bind(this);
    this.openPortal = this.openPortal.bind(this);
    this.closeLoginModal = this.closeLoginModal.bind(this);
    this.closeRegisterModal = this.closeRegisterModal.bind(this);
    this.closePortal = this.closePortal.bind(this);
  }

  openPortal(header: string, message: string, color: SemanticCOLORS) {
    if (this.state.portal.open) {
      this.setState({
        portal: {
          open: false
        }
      } as LayoutState);
    }
    process.nextTick(() => {
      this.setState({
        portal: {
          open: true,
          header: header,
          message: message,
          color: color
        }
      } as LayoutState);
    })
  }

  login() {
    this.setState({
      loginModalOpen: true
    } as LayoutState);
  }

  register() {
    this.setState({
      registerModalOpen: true
    } as LayoutState);
  }

  logout() {
    Post('/Account/Logout').then(res => res.json()).then(data => {
      let result = data as ResultModel;
      if (result.succeeded) {
        this.openPortal('提示', '退出成功', 'green');
        this.props.refreshUserInfo();
      }
      else {
        this.openPortal('错误', `退出失败\n${result.errorMessage} (${result.errorCode})`, 'red');
      }
    })
      .catch(err => {
        this.openPortal('错误', '退出失败', 'red');
        console.log(err);
      })
  }

  closeLoginModal() {
    this.setState({
      loginModalOpen: false
    } as LayoutState);
  }

  closeRegisterModal() {
    this.setState({
      registerModalOpen: false
    } as LayoutState);
  }

  closePortal() {
    this.setState({
      portal: {
        open: false
      }
    } as LayoutState);
  }

  render() {
    let accountOptions = this.props.userInfo.signedIn ? <Dropdown text='账户' floating>
      <Dropdown.Menu>
        <Dropdown.Item icon='home' text='门户' />
        <Dropdown.Item icon='sign out' text='退出' onClick={this.logout} />
      </Dropdown.Menu>
    </Dropdown> : <Dropdown text='账户' floating>
        <Dropdown.Menu>
          <Dropdown.Item icon='sign in' text='登录' onClick={this.login} />
          <Dropdown.Item icon='signup' text='注册' onClick={this.register} />
        </Dropdown.Menu>
      </Dropdown>;

    return (
      <>
        <Menu fixed='top' size='small' borderless inverted color='blue' compact icon='labeled'>
          <Container>
            <NavLink exact to='/' className='item'><Icon name='h'></Icon>主页</NavLink>
            <NavLink to='/problem' className='item'><Icon name='code'></Icon>题库</NavLink>
            <NavLink to='/contest' className='item'><Icon name='pencil'></Icon>比赛</NavLink>
            <NavLink to='/group' className='item'><Icon name='users'></Icon>小组</NavLink>
            <NavLink to='/status' className='item'><Icon name='list'></Icon>状态</NavLink>
            <NavLink to='/rank' className='item'><Icon name='list ol'></Icon>排名</NavLink>
            <NavLink to='/message' className='item'><Icon name='paper plane'></Icon>消息</NavLink>
            <NavLink to='/discussion' className='item'><Icon name='discussions'></Icon>讨论</NavLink>
            <NavLink to='/article' className='item'><Icon name='book'></Icon>文章</NavLink>
            <NavLink to='/about' className='item'><Icon name='info circle'></Icon>关于</NavLink>
            <Menu.Item position='right'>
              <Icon name='user'></Icon>
              {accountOptions}
            </Menu.Item>
          </Container>
        </Menu>
        <Container style={{ marginTop: '7em' }}>
          {this.props.children}
        </Container>
        <Menu fixed='bottom' inverted color='blue' borderless>
          <Container>
            <Menu.Item>
              H::Judge &copy; {new Date().getFullYear()}
            </Menu.Item>
            <Menu.Item position='right'>
              hez2010 All Rights Reserved.
            </Menu.Item>
          </Container>
        </Menu>
        <Login modalOpen={this.state.loginModalOpen} closeModal={this.closeLoginModal} refreshUserInfo={this.props.refreshUserInfo} openPortal={this.openPortal} />
        <Register modalOpen={this.state.registerModalOpen} closeModal={this.closeRegisterModal} refreshUserInfo={this.props.refreshUserInfo} openPortal={this.openPortal} />
        <TransitionablePortal open={this.state.portal.open} onClose={this.closePortal} transition={{ animation: 'drop', duration: 500 }}>
          <Segment style={{ bottom: '5em', position: 'fixed', right: '2em', zIndex: 1048576 }} color={this.state.portal.color} inverted>
            <Header>
              {this.state.portal.header}
              <div style={{ display: 'inline', cursor: 'pointer', float: 'right' }} onClick={this.closePortal}>
                <Icon name='close' size='small'></Icon>
              </div>
            </Header>
            <Divider />
            <p style={{ wordBreak: 'break-all', wordWrap: 'break-word', 'overflow': 'hidden', width: '20em' }}>{this.state.portal.message}</p>
          </Segment>
        </TransitionablePortal>
      </>
    );
  }
}